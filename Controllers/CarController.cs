using Entry.Concrete;
using Entry.Data;
using Entry.Dto;
using Entry.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;

namespace Entry.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CarsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CarsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        var query = _db.Cars
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Images)
            .Include(x => x.Features)
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CarDetailsDto
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                Title = x.Title,
                Brand = x.Brand,
                Model = x.Model,
                Year = x.Year,
                Price = x.Price,
                UserId = x.UserId,
                Images = x.Images.OrderBy(i => i.Order).Select(i => new CarImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    IsMain = i.IsMain,
                    Order = i.Order
                }).ToList(),
                Features = x.Features.Select(f => new CarFeatureDto
                {
                    Id = f.Id,
                    Key = f.Key,
                    Value = f.Value
                }).ToList()
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var car = await _db.Cars
            .AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.Features)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (car is null) return NotFound();

        var dto = new CarDetailsDto
        {
            Id = car.Id,
            CreatedAt = car.CreatedAt,
            Title = car.Title,
            Brand = car.Brand,
            Model = car.Model,
            Year = car.Year,
            Price = car.Price,
            UserId = car.UserId,
            Images = car.Images.OrderBy(i => i.Order).Select(i => new CarImageDto
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                IsMain = i.IsMain,
                Order = i.Order
            }).ToList(),
            Features = car.Features.Select(f => new CarFeatureDto
            {
                Id = f.Id,
                Key = f.Key,
                Value = f.Value
            }).ToList()
        };

        return Ok(dto);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCarDto dto)
    {
        if (!User.IsInRole("Admin") && await IsCurrentUserBlocked())
            return Forbid("Siz bloklanmısınız.");

        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Title boş ola bilməz");
        if (string.IsNullOrWhiteSpace(dto.Brand)) return BadRequest("Brand boş ola bilməz");
        if (string.IsNullOrWhiteSpace(dto.Model)) return BadRequest("Model boş ola bilməz");
        if (dto.Year < 1950 || dto.Year > DateTime.UtcNow.Year + 1) return BadRequest("Year düzgün deyil");
        if (dto.Price <= 0) return BadRequest("Price 0-dan böyük olmalıdır");

        var userId = User.GetUserId();

        var features = (dto.Features ?? new List<CreateCarFeatureDto>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.Value))
            .Select(f => new CarFeature
            {
                Key = f.Key.Trim(),
                Value = f.Value.Trim(),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        var car = new Car
        {
            IsActive = true,
            Title = dto.Title.Trim(),
            Brand = dto.Brand.Trim(),
            Model = dto.Model.Trim(),
            Year = dto.Year,
            Price = dto.Price,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Features = features
        };

        _db.Cars.Add(car);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = car.Id }, new { id = car.Id });
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!User.IsInRole("Admin") && await IsCurrentUserBlocked())
            return Forbid("Siz bloklanmısınız.");

        var car = await _db.Cars.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (car is null) return NotFound();

        var userId = User.GetUserId();
        if (!User.IsInRole("Admin") && car.UserId != userId)
            return Forbid();

        car.IsActive = false; // soft delete
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:int}/images")]
    public async Task<IActionResult> UploadImages(int id, [FromForm] List<IFormFile> files)
    {
        if (!User.IsInRole("Admin") && await IsCurrentUserBlocked())
            return Forbid("Siz bloklanmısınız.");

        if (files == null || files.Count == 0)
            return BadRequest("Heç bir şəkil göndərilməyib.");

        var car = await _db.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (car is null) return NotFound("Elan tapılmadı.");

        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && car.UserId != userId) return Forbid();

        var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var carFolder = Path.Combine(webRoot, "uploads", "cars", car.Id.ToString());
        Directory.CreateDirectory(carFolder);

        var nextOrder = car.Images.Any() ? car.Images.Max(i => i.Order) + 1 : 0;
        var hasMain = car.Images.Any(i => i.IsMain);

        foreach (var file in files)
        {
            if (file is null || file.Length <= 0) continue;

            // 5MB limit
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest("Şəkil 5MB-dan böyük ola bilməz.");

            if (!file.ContentType.StartsWith("image/"))
                return BadRequest("Yalnız şəkil faylları qəbul olunur.");

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            ext = ext.ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
                return BadRequest("Yalnız jpg, jpeg, png, webp qəbul olunur.");

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(carFolder, fileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/cars/{car.Id}/{fileName}";

            var img = new CarImage
            {
                CarId = car.Id,
                ImageUrl = url,
                IsMain = !hasMain,
                Order = nextOrder++
            };

            hasMain = true;
            _db.CarImages.Add(img);
        }

        await _db.SaveChangesAsync();

        var images = await _db.CarImages
            .AsNoTracking()
            .Where(i => i.CarId == car.Id)
            .OrderBy(i => i.Order)
            .Select(i => new Entry.Dto.CarImageDto
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                IsMain = i.IsMain,
                Order = i.Order
            })
            .ToListAsync();

        return Ok(new { carId = car.Id, images });
    }

    [Authorize]
    [HttpPut("{id:int}/images/{imageId:int}/main")]
    public async Task<IActionResult> SetMainImage(int id, int imageId)
    {
        if (!User.IsInRole("Admin") && await IsCurrentUserBlocked())
            return Forbid("Siz bloklanmısınız.");

        var car = await _db.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (car is null) return NotFound("Elan tapılmadı.");

        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && car.UserId != userId) return Forbid();

        var target = car.Images.FirstOrDefault(i => i.Id == imageId);
        if (target is null) return NotFound("Şəkil tapılmadı.");

        foreach (var img in car.Images)
            img.IsMain = (img.Id == imageId);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        if (!User.IsInRole("Admin") && await IsCurrentUserBlocked())
            return Forbid("Siz bloklanmısınız.");

        var car = await _db.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (car is null) return NotFound("Elan tapılmadı.");

        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && car.UserId != userId) return Forbid();

        var img = car.Images.FirstOrDefault(i => i.Id == imageId);
        if (img is null) return NotFound("Şəkil tapılmadı.");

        var physicalPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            img.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
        );

        if (System.IO.File.Exists(physicalPath))
            System.IO.File.Delete(physicalPath);

        _db.CarImages.Remove(img);
        await _db.SaveChangesAsync();

        if (img.IsMain)
        {
            var first = await _db.CarImages
                .Where(i => i.CarId == id)
                .OrderBy(i => i.Order)
                .FirstOrDefaultAsync();

            if (first != null)
            {
                first.IsMain = true;
                await _db.SaveChangesAsync();
            }
        }

        return NoContent();
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCarDto dto)
    {
        if (!User.IsInRole("Admin") && await IsCurrentUserBlocked())
            return Forbid("Siz bloklanmısınız.");

        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Title boş ola bilməz");
        if (string.IsNullOrWhiteSpace(dto.Brand)) return BadRequest("Brand boş ola bilməz");
        if (string.IsNullOrWhiteSpace(dto.Model)) return BadRequest("Model boş ola bilməz");
        if (dto.Year < 1950 || dto.Year > DateTime.UtcNow.Year + 1) return BadRequest("Year düzgün deyil");
        if (dto.Price <= 0) return BadRequest("Price 0-dan böyük olmalıdır");

        var car = await _db.Cars
            .Include(c => c.Features)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (car is null) return NotFound("Elan tapılmadı.");

        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && car.UserId != userId) return Forbid();

        car.Title = dto.Title.Trim();
        car.Brand = dto.Brand.Trim();
        car.Model = dto.Model.Trim();
        car.Year = dto.Year;
        car.Price = dto.Price;

        _db.CarFeatures.RemoveRange(car.Features);

        car.Features = (dto.Features ?? new List<CreateCarFeatureDto>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.Value))
            .Select(f => new CarFeature
            {
                Key = f.Key.Trim(),
                Value = f.Value.Trim(),
                CarId = car.Id,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPut("{id:int}/images/reorder")]
    public async Task<IActionResult> ReorderImages(int id, [FromBody] ReorderImagesDto dto)
    {
        if (!User.IsInRole("Admin") && await IsCurrentUserBlocked())
            return Forbid("Siz bloklanmısınız.");

        if (dto.ImageIds == null || dto.ImageIds.Count == 0)
            return BadRequest("ImageIds boş ola bilməz.");

        var car = await _db.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (car is null) return NotFound("Elan tapılmadı.");

        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && car.UserId != userId) return Forbid();

        var carImageIds = car.Images.Select(i => i.Id).ToHashSet();
        if (dto.ImageIds.Any(x => !carImageIds.Contains(x)))
            return BadRequest("Göndərilən şəkillər bu elana aid deyil.");

        for (int idx = 0; idx < dto.ImageIds.Count; idx++)
        {
            var imageId = dto.ImageIds[idx];
            var img = car.Images.First(i => i.Id == imageId);
            img.Order = idx;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> IsCurrentUserBlocked()
    {
        var userId = User.GetUserId();
        return await _db.Users.AnyAsync(u => u.Id == userId && u.IsBlocked);
    }
}
