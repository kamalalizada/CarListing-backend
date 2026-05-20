using Entry.Concrete;
using Entry.Data;
using Entry.Dto;
using Entry.Extensions;
using Entry.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Entry.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CarsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IImageStorageService _imageStorage;
    private readonly IAiSearchParserService _aiSearchParser;


    public CarsController(AppDbContext db, IImageStorageService imageStorage, IAiSearchParserService aiSearchParser)
    {
        _db = db;
        _imageStorage = imageStorage;
        _aiSearchParser = aiSearchParser;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<CarListItemDto>>> GetAll([FromQuery] CarSearchFilterDto filter)
    {
        if (filter.Page <= 0) filter.Page = 1;
        if (filter.PageSize <= 0 || filter.PageSize > 100) filter.PageSize = 20;

        var query = _db.Cars
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Images)
            .Include(x => x.Features)
            .AsQueryable();

        query = ApplyFilters(query, filter);
        query = ApplySorting(query, filter);

        var total = await query.CountAsync();

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new CarListItemDto
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                Title = x.Title,
                Brand = x.Brand,
                Model = x.Model,
                Year = x.Year,
                Price = x.Price,
                Mileage = x.Mileage,
                FuelType = x.FuelType,
                BodyType = x.BodyType,
                Transmission = x.Transmission,
                Engine = x.Engine,
                City = x.City,
                Color = x.Color,
                Features = x.Features.Select(f => new CarFeatureDto
                {
                    Id = f.Id,
                    Key = f.Key,
                    Value = f.Value
                }).ToList()
            })
            .ToListAsync();

        return Ok(new PagedResultDto<CarListItemDto>
        {
            Total = total,
            Page = filter.Page,
            PageSize = filter.PageSize,
            Items = items
        });
    }

    [HttpGet("metadata")]
    public ActionResult<CarMetadataDto> GetMetadata()
    {
        return Ok(new CarMetadataDto
        {
            FuelTypes = ["Petrol", "Diesel", "Hybrid", "Electric"],
            BodyTypes = ["Sedan", "SUV", "Hatchback", "Coupe", "Wagon", "Minivan", "Pickup"],
            Transmissions = ["Automatic", "Manual"],
            SortOptions = ["newest", "price", "year", "mileage"]
        });
    }

    [HttpGet("compare")]
    public async Task<ActionResult<List<CarCompareItemDto>>> Compare([FromQuery] List<int> ids)
    {
        var distinctIds = ids.Distinct().Take(4).ToList();
        if (distinctIds.Count < 2)
            return BadRequest("Müqayisə üçün ən azı 2 elan seçilməlidir.");

        var items = await _db.Cars
            .AsNoTracking()
            .Where(c => c.IsActive && distinctIds.Contains(c.Id))
            .Include(c => c.Features)
            .Select(c => new CarCompareItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Brand = c.Brand,
                Model = c.Model,
                Year = c.Year,
                Price = c.Price,
                Mileage = c.Mileage,
                FuelType = c.FuelType,
                BodyType = c.BodyType,
                Transmission = c.Transmission,
                Engine = c.Engine,
                City = c.City,
                Color = c.Color,
                Features = c.Features.Select(f => new CarFeatureDto
                {
                    Id = f.Id,
                    Key = f.Key,
                    Value = f.Value
                }).ToList()
            })
            .ToListAsync();

        var orderedItems = distinctIds
            .Join(items, id => id, item => item.Id, (_, item) => item)
            .ToList();

        if (orderedItems.Count != distinctIds.Count)
            return BadRequest("Seçilən elanlardan biri tapılmadı və ya aktiv deyil.");

        return Ok(orderedItems);
    }

    [HttpPost("ai-search/parse")]
    public ActionResult<AiSearchResultDto> ParseAiSearch([FromBody] AiSearchRequestDto request)
    {
        return Ok(_aiSearchParser.Parse(request));
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
            Mileage = car.Mileage,
            FuelType = car.FuelType,
            BodyType = car.BodyType,
            Transmission = car.Transmission,
            Engine = car.Engine,
            City = car.City,
            Color = car.Color,
            Description = car.Description,
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
            Mileage = dto.Mileage,
            FuelType = dto.FuelType.Trim(),
            BodyType = dto.BodyType.Trim(),
            Transmission = dto.Transmission.Trim(),
            Engine = dto.Engine.Trim(),
            City = dto.City.Trim(),
            Color = NormalizeOptional(dto.Color),
            Description = NormalizeOptional(dto.Description),
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
        if (files == null || files.Count == 0)
            return BadRequest("Heç bir şəkil göndərilməyib.");

        var car = await _db.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (car is null) return NotFound("Elan tapılmadı.");

        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && car.UserId != userId) return Forbid();

        var nextOrder = car.Images.Any() ? car.Images.Max(i => i.Order) + 1 : 0;
        var hasMain = car.Images.Any(i => i.IsMain);

        foreach (var file in files)
        {
            if (file is null || file.Length <= 0) continue;

            // 5MB limit
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest("Şəkil 5MB-dan böyük ola bilməz.");

            if (!ImageFileValidator.IsAllowedContentType(file.ContentType))
                return BadRequest("Yalnız jpeg, png, webp və avif şəkilləri qəbul olunur.");

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            ext = ext.ToLowerInvariant();
            if (!ImageFileValidator.IsAllowedExtension(ext))
                return BadRequest("Yalnız jpg, jpeg, png, avif  webp qəbul olunur.");

            await using var stream = file.OpenReadStream();
            if (!ImageFileValidator.HasMatchingSignature(stream, ext))
                return BadRequest("Yüklənən faylın formatı etibarlı şəkil formatı deyil.");

            var upload = await _imageStorage.UploadAsync(
                car.Id,
                stream,
                file.Length,
                ext,
                file.ContentType,
                cancellationToken: HttpContext.RequestAborted);

            var img = new CarImage
            {
                CarId = car.Id,
                ImageUrl = upload.ImageUrl,
                ObjectKey = upload.ObjectKey,
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
        var car = await _db.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (car is null) return NotFound("Elan tapılmadı.");

        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && car.UserId != userId) return Forbid();

        var img = car.Images.FirstOrDefault(i => i.Id == imageId);
        if (img is null) return NotFound("Şəkil tapılmadı.");

        var objectKey = img.ObjectKey ?? _imageStorage.TryGetObjectKeyFromUrl(img.ImageUrl);
        await _imageStorage.DeleteAsync(objectKey, HttpContext.RequestAborted);

        var wasMain = img.IsMain;

        _db.CarImages.Remove(img);
        await _db.SaveChangesAsync();

        // Əgər main şəkil silinibsə, yenisini main et
        if (wasMain)
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
        car.Mileage = dto.Mileage;
        car.FuelType = dto.FuelType.Trim();
        car.BodyType = dto.BodyType.Trim();
        car.Transmission = dto.Transmission.Trim();
        car.Engine = dto.Engine.Trim();
        car.City = dto.City.Trim();
        car.Color = NormalizeOptional(dto.Color);
        car.Description = NormalizeOptional(dto.Description);

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

    private static IQueryable<Car> ApplyFilters(IQueryable<Car> query, CarSearchFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.Title.Contains(search) ||
                x.Brand.Contains(search) ||
                x.Model.Contains(search) ||
                x.City.Contains(search) ||
                x.FuelType.Contains(search) ||
                x.BodyType.Contains(search) ||
                x.Transmission.Contains(search) ||
                x.Engine.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Brand))
            query = query.Where(x => x.Brand == filter.Brand.Trim());

        if (!string.IsNullOrWhiteSpace(filter.Model))
            query = query.Where(x => x.Model == filter.Model.Trim());

        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(x => x.City == filter.City.Trim());

        if (!string.IsNullOrWhiteSpace(filter.FuelType))
            query = query.Where(x => x.FuelType == filter.FuelType.Trim());

        if (!string.IsNullOrWhiteSpace(filter.BodyType))
            query = query.Where(x => x.BodyType == filter.BodyType.Trim());

        if (!string.IsNullOrWhiteSpace(filter.Transmission))
            query = query.Where(x => x.Transmission == filter.Transmission.Trim());

        if (filter.PriceMin.HasValue)
            query = query.Where(x => x.Price >= filter.PriceMin.Value);

        if (filter.PriceMax.HasValue)
            query = query.Where(x => x.Price <= filter.PriceMax.Value);

        if (filter.YearMin.HasValue)
            query = query.Where(x => x.Year >= filter.YearMin.Value);

        if (filter.YearMax.HasValue)
            query = query.Where(x => x.Year <= filter.YearMax.Value);

        if (filter.MileageMax.HasValue)
            query = query.Where(x => x.Mileage <= filter.MileageMax.Value);

        return query;
    }

    private static IQueryable<Car> ApplySorting(IQueryable<Car> query, CarSearchFilterDto filter)
    {
        var descending = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return filter.SortBy?.Trim().ToLowerInvariant() switch
        {
            "price" => descending ? query.OrderByDescending(x => x.Price) : query.OrderBy(x => x.Price),
            "year" => descending ? query.OrderByDescending(x => x.Year) : query.OrderBy(x => x.Year),
            "mileage" => descending ? query.OrderByDescending(x => x.Mileage) : query.OrderBy(x => x.Mileage),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [Authorize]
    [HttpPut("{id:int}/images/reorder")]
    public async Task<IActionResult> ReorderImages(int id, [FromBody] ReorderImagesDto dto)
    {
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
        var requestedImageIds = dto.ImageIds.ToHashSet();

        if (requestedImageIds.Count != dto.ImageIds.Count)
            return BadRequest("Şəkil ID-ləri təkrar göndərilə bilməz.");

        if (requestedImageIds.Count != carImageIds.Count || !requestedImageIds.SetEquals(carImageIds))
            return BadRequest("Göndərilən şəkillər bu elana aid deyil.");

        var imagesById = car.Images.ToDictionary(i => i.Id);
        for (int idx = 0; idx < dto.ImageIds.Count; idx++)
        {
            var imageId = dto.ImageIds[idx];
            var img = imagesById[imageId];
            img.Order = idx;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
