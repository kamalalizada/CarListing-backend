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
                // Translated to a correlated subquery inside this one SELECT,
                // so a page of cards costs one round trip, not one per card.
                MainImageUrl = x.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Order)
                    .ThenBy(i => i.Id)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
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

    /// <summary>
    /// The values a filter panel can offer, read from the live listings.
    /// </summary>
    /// <remarks>
    /// Read from the data rather than hard-coded: a dropdown offering a brand
    /// nobody has listed sends the buyer to an empty page, and a fixed list
    /// goes stale the first time someone posts something new. Only active
    /// listings count, for the same reason.
    /// </remarks>
    [HttpGet("metadata")]
    public async Task<ActionResult<CarMetadataDto>> GetMetadata(CancellationToken ct)
    {
        // One pass over the columns that feed the dropdowns, rather than a
        // query per list.
        var rows = await _db.Cars
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new
            {
                c.Brand,
                c.Model,
                c.City,
                c.FuelType,
                c.BodyType,
                c.Transmission,
            })
            .ToListAsync(ct);

        static List<string> Sorted(IEnumerable<string?> values) => values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.CurrentCulture)
            .ToList();

        return Ok(new CarMetadataDto
        {
            Brands = Sorted(rows.Select(r => r.Brand)),
            Models = Sorted(rows.Select(r => r.Model)),

            // Grouped so the panel can narrow the model dropdown once a brand
            // is chosen; a flat list would put Sonata under BMW.
            ModelsByBrand = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Brand) && !string.IsNullOrWhiteSpace(r.Model))
                .GroupBy(r => r.Brand.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.CurrentCulture)
                .ToDictionary(
                    g => g.Key,
                    g => Sorted(g.Select(r => r.Model)),
                    StringComparer.OrdinalIgnoreCase),

            Cities = Sorted(rows.Select(r => r.City)),
            FuelTypes = Sorted(rows.Select(r => r.FuelType)),
            BodyTypes = Sorted(rows.Select(r => r.BodyType)),
            Transmissions = Sorted(rows.Select(r => r.Transmission)),

            // Not data: these are the values the listing endpoint accepts.
            SortFields = ["newest", "price", "year", "mileage"],
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
                MainImageUrl = c.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Order)
                    .ThenBy(i => i.Id)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
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
    public async Task<ActionResult<AiSearchResultDto>> ParseAiSearch(
        [FromBody] AiSearchRequestDto request,
        CancellationToken ct)
    {
        return Ok(await _aiSearchParser.ParseAsync(request, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var car = await _db.Cars
            .AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.Features)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (car is null) return NotFound();

        // An inactive listing (admin-deactivated or soft-deleted) is hidden from
        // the public as if it did not exist; only its owner and admins see it.
        // The endpoint is anonymous, but the default JWT scheme still populates
        // User when a valid token is sent.
        if (!car.IsActive && !CanManage(car))
            return NotFound();

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
            ContactPhone = car.ContactPhone,
            Color = car.Color,
            Description = car.Description,
            UserId = car.UserId,
            IsActive = car.IsActive,
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
            ContactPhone = NormalizePhone(dto.ContactPhone),
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
        car.ContactPhone = NormalizePhone(dto.ContactPhone);
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

    /// <summary>True when the caller is signed in and is the car's owner or an admin.</summary>
    private bool CanManage(Car car)
    {
        if (User.Identity?.IsAuthenticated != true) return false;
        if (User.IsInRole("Admin")) return true;

        try
        {
            return User.GetUserId() == car.UserId;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizePhone(string value)
    {
        return string.Concat(value.Where(c => !char.IsWhiteSpace(c) && c != '-'));
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
