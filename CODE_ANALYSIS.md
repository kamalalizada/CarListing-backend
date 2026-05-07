# CarListing Kod Analizi

Tarix: 2026-05-01  
Layihə tipi: ASP.NET Core 8 Web API

## Qısa xülasə

Bu layihə avtomobil elanları üçün backend API-dir. İstifadəçi qeydiyyatı və login, JWT token ilə autentifikasiya, admin rolları, elan CRUD əməliyyatları, elanlara dinamik xüsusiyyət əlavə etmə, şəkil yükləmə/silmə/sıralama və MinIO obyekt storage inteqrasiyası işlədilib.

Layihə işlək ideyaya malikdir, amma production üçün hazır deyil. Əsas risklər: hardcoded secret-lər, avtomatik admin seed parolu, fərqli password hashing formatları, migration/model uyğunsuzluğu, build-in uğursuz qayıtması, input validation zəifliyi və MinIO bucket policy-nin hər upload/delete zamanı təkrar tətbiq olunmasıdır.

## Layihə strukturu

```text
CarListing.sln
├── wCarListing.csproj          # Əsas ASP.NET Core Web API
├── Program.cs                  # DI, middleware, auth, migration, seed
├── Controllers/
│   ├── AuthController.cs       # Register/login
│   ├── CarController.cs        # Elan və şəkil əməliyyatları
│   └── AdminController.cs      # Admin əməliyyatları
├── Entry/
│   ├── Concrete/               # Entity modellər
│   └── Dto/                    # Request/response DTO-lar
├── DataAccess/
│   ├── AppDbContext.cs         # EF Core model konfiqurasiyası
│   └── Migrations/             # DB migration-lar
├── Business/
│   └── Concrete/               # TokenService, PasswordService
├── Services/                   # MinIO və lokal şəkil migrasiyası
└── wwwroot/uploads/            # Köhnə/lokal şəkil faylları
```

## İstifadə olunan texnologiyalar

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8
- SQL Server LocalDB
- JWT Bearer Authentication
- Swagger / Swashbuckle
- MinIO object storage
- BCrypt.Net-Next paketi
- PBKDF2 password hashing
- Docker Compose ilə MinIO

## Layihədə nələr işlədilib

### Authentication

`AuthController` iki endpoint verir:

- `POST /api/auth/register`
- `POST /api/auth/login`

Register zamanı username, email və password qəbul olunur. Email lowercase edilir, user DB-yə yazılır və JWT token qaytarılır. Login zamanı email/password yoxlanır, user bloklanıbsa girişə icazə verilmir.

JWT token `TokenService` tərəfindən yaradılır. Token-ə bu claim-lər əlavə edilir:

- `NameIdentifier`: user id
- `Name`: username
- `Role`: user role

### Authorization

`[Authorize]` elan yaratmaq, silmək, yeniləmək və şəkil əməliyyatlarında istifadə olunur. Admin endpoint-ləri `[Authorize(Roles = "Admin")]` ilə qorunur.

Normal istifadəçi yalnız öz elanını dəyişə/silə bilər. Admin bütün elanlara müdaxilə edə bilir.

### Car API

`CarsController` aşağıdakı funksionalları verir:

- Aktiv elanların paginated siyahısı
- Elan detallarını ID ilə almaq
- Elan yaratmaq
- Elanı soft delete etmək
- Elanı update etmək
- Şəkil yükləmək
- Əsas şəkli seçmək
- Şəkli silmək
- Şəkillərin sırasını dəyişmək

Elan silinərkən DB-dən fiziki silinmir, `IsActive = false` edilir.

### Admin API

`AdminController` bu əməliyyatları verir:

- User bloklama/blokdan çıxarma
- Elanı aktiv/deaktiv etmək
- Bütün elanları admin üçün siyahılamaq

### Database

Əsas entity-lər:

- `User`
- `Car`
- `CarImage`
- `CarFeature`

`AppDbContext`-də unique index-lər, foreign key-lər, cascade delete və max length-lər konfiqurasiya olunub.

### Şəkil storage

Yeni şəkillər MinIO-ya yüklənir. `CarImage` cədvəlində həm public `ImageUrl`, həm də MinIO üçün `ObjectKey` saxlanılır. Lokal `/uploads/...` şəkilləri MinIO-ya köçürmək üçün `--migrate-local-images` argmenti ilə işləyən `LocalImageMigrationService` var.

## API endpoint xülasəsi

| Method | URL | Auth | Məqsəd |
|---|---|---:|---|
| POST | `/api/auth/register` | Yox | User qeydiyyatı |
| POST | `/api/auth/login` | Yox | Login və token almaq |
| GET | `/api/cars` | Yox | Aktiv elan siyahısı |
| GET | `/api/cars/{id}` | Yox | Elan detalları |
| POST | `/api/cars` | Var | Elan yaratmaq |
| PUT | `/api/cars/{id}` | Var | Elan update |
| DELETE | `/api/cars/{id}` | Var | Soft delete |
| POST | `/api/cars/{id}/images` | Var | Şəkil yükləmək |
| PUT | `/api/cars/{id}/images/{imageId}/main` | Var | Main şəkil seçmək |
| DELETE | `/api/cars/{id}/images/{imageId}` | Var | Şəkil silmək |
| PUT | `/api/cars/{id}/images/reorder` | Var | Şəkil sırası |
| PUT | `/api/admin/users/{id}/block` | Admin | User bloklama |
| PUT | `/api/admin/cars/{id}/active` | Admin | Elan statusu |
| GET | `/api/admin/cars` | Admin | Admin elan siyahısı |

## Əsas problemlər və risklər

### 1. Build uğursuz qayıdır

Yoxlama:

```text
dotnet restore CarListing.sln
```

uğurlu oldu.

Amma:

```text
dotnet build CarListing.sln --no-restore
dotnet build wCarListing.csproj --no-restore -v:minimal
```

ikisi də `Oluşturma BAŞARISIZ OLDU` ilə qayıtdı, amma `0 Uyarı, 0 Hata` göstərdi. `build.log` içində MSBuild workload SDK resolver problemləri görünür:

- `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator`
- `Microsoft.NET.SDK.WorkloadManifestTargetsLocator`

Bu kod səhvindən çox lokal .NET SDK/workload vəziyyətinə oxşayır. Yenə də build exit code `1` olduğu üçün CI/CD-də problem yaradacaq.

### 2. Hardcoded secret və credential-lər var

`appsettings.json` içində bunlar açıq yazılıb:

- SQL connection string
- JWT secret key
- MinIO access key
- MinIO secret key

Bu təhlükəlidir. Secret-lər `User Secrets`, environment variable və ya deployment secret manager ilə verilməlidir.

### 3. Default admin parolu kodda hardcoded-dir

`Program.cs` içində admin avtomatik yaradılır:

```text
Username = "admin"
Email = "admin@local.test"
Password = "Admin123!"
```

Bu production mühitdə çox ciddi riskdir. İlk deploy zamanı hər kəs bu default credential-i təxmin edə bilər. Admin seed ya tam silinməli, ya da parol environment variable-dan gəlməli və ilk login-də dəyişdirilməlidir.

### 4. Password hashing formatı qarışıqdır

Normal register `PasswordService.Hash` ilə PBKDF2 hash yaradır. Amma default admin `BCrypt.Net.BCrypt.HashPassword` ilə yaradılır.

Login isə yalnız `PasswordService.Verify` çağırır. Bu o deməkdir ki, seed edilən admin istifadəçi BCrypt hash ilə saxlandığı üçün login zamanı PBKDF2 formatına uyğun gəlmir və admin login işləməyə bilər.

Problem yerləri:

- `Program.cs:113` BCrypt ilə admin hash
- `AuthController.cs:65` PBKDF2 verify

Həll: ya admin də `PasswordService.Hash("Admin123!")` ilə yaradılmalıdır, ya da `PasswordService.Verify` BCrypt və PBKDF2 formatlarını birlikdə dəstəkləməlidir.

### 5. Migration-lar uyğunsuz görünür

`InitialMinioStorage` migration artıq `CarImages.ObjectKey` və `ImageUrl nvarchar(500)` yaradır. Sonrakı `AddObjectKeyToCarImages` migration isə yenidən `ObjectKey` əlavə edir və `ImageUrl`-u `nvarchar(255)`-dən `nvarchar(500)`-ə dəyişməyə çalışır.

Əgər DB sıfırdan qurulursa, ikinci migration `ObjectKey` artıq mövcuddur deyə fail edə bilər. `Program.cs` içində `EnsureMinioImageColumnsAsync` adlı manual SQL patch bunu kompensasiya etməyə çalışır, amma migration tarixçəsini düzəltmir.

Bu, migration-ların əl ilə dəyişdirildiyini və EF migration zəncirinin etibarsızlaşdığını göstərir.

### 6. Manual schema patch migration əvəzinə işlədilir

`Program.cs:160`-da `EnsureMinioImageColumnsAsync` var. Bu funksiya runtime-da SQL `ALTER TABLE` icra edir.

Bu yanaşma qısa müddətli fix kimi işləyə bilər, amma production üçün yaxşı deyil. Schema dəyişiklikləri migration ilə idarə olunmalıdır. Runtime-da schema dəyişmək deploy zamanı gözlənilməz lock, permission və rollback problemləri yarada bilər.

### 7. Input validation zəifdir və null exception verə bilər

`AuthController.Register` metodunda `dto.Username.Trim()` və `dto.Email.Trim()` validation-dan əvvəl çağırılır. Əgər `Username` və ya `Email` null gəlsə, API `400 BadRequest` əvəzinə exception verə bilər.

Problem yerləri:

- `AuthController.cs:27`
- `AuthController.cs:28`
- `AuthController.cs:58`

Eyni risk başqa DTO string-lərində də var. DataAnnotations və ya FluentValidation istifadə etmək daha sağlamdır.

### 8. Şəkil content validation kifayət deyil

Upload zamanı yoxlama əsasən bunlara baxır:

- `file.ContentType.StartsWith("image/")`
- file extension whitelist
- 5MB limit

`ContentType` client tərəfindən göndərildiyi üçün etibarlı deyil. Faylın real magic bytes/header yoxlanmalıdır. Yoxsa zərərli fayl image kimi göndərilə bilər.

Problem yerləri:

- `CarController.cs:206`
- `CarController.cs:213`

### 9. MinIO bucket policy hər əməliyyatda set edilir

`MinioImageStorageService.EnsureBucketAsync` hər upload/delete zamanı bucket mövcudluğunu yoxlayır və public read policy-ni yenidən tətbiq edir.

Problem yeri:

- `Services/MinioImageStorageService.cs:104`

Bu performans və permission baxımından yaxşı deyil. Bucket yaradılması və policy tətbiqi startup/provisioning mərhələsində edilməlidir.

### 10. Public bucket security riski

MinIO policy bütün obyektlərə public `s3:GetObject` icazəsi verir. Əgər bütün şəkillər həqiqətən publicdirsə, bu qəbul edilə bilər. Amma user upload-ları və gələcək private data üçün risklidir.

Alternativ:

- Public yalnız şəkil bucket-i üçün qalsın
- Private obyektlər üçün presigned URL istifadə edilsin
- Fayl adları və path-lər predictable olmasın

Hazırda yeni upload-lar GUID ilə yaradıldığı üçün ad təxmin etmək çətindir, amma bucket ümumilikdə public-dir.

### 11. Reorder endpoint duplicate id-ləri yoxlamır

`ReorderImages` yalnız göndərilən ID-lərin həmin elana aid olub-olmadığını yoxlayır. Amma duplicate ID göndərilsə, sıralama qeyri-dəqiq ola bilər və bəzi şəkillərin order-i köhnə qala bilər.

Problem yerləri:

- `CarController.cs:383`
- `CarController.cs:396`
- `CarController.cs:400`

Həll: `dto.ImageIds.Distinct().Count() == car.Images.Count` və set bərabərliyi yoxlanmalıdır.

### 12. Soft delete şəkilləri storage-dan silmir

Elan silinəndə yalnız `IsActive = false` edilir. MinIO-dakı şəkillər qalır. Bu biznes qərarı ola bilər, amma storage təmizliyi və GDPR kimi tələblər üçün problem yarada bilər.

Problem yeri:

- `CarController.cs:169`

Əgər soft delete saxlanmalıdırsa, ayrıca cleanup job və ya admin purge endpoint lazımdır.

### 13. Block edilmiş user-in mövcud tokenləri aktiv qalır

Login zamanı bloklanmış user yoxlanır. Protected car əməliyyatlarında da `IsCurrentUserBlocked` yoxlanır. Amma bu yoxlama bütün gələcək endpoint-lərdə unudula bilər. Token özü 7 gün aktiv qalır.

Daha yaxşı həll:

- JWT validation event-də user statusunu yoxlamaq
- Token lifetime-ı azaltmaq
- Refresh token/revocation mexanizmi əlavə etmək

### 14. DTO-larda DataAnnotations yoxdur

Hazırda validation controller metodlarının içində manual yazılıb. Bu, təkrarçılıq və unudulma riski yaradır.

Məsələn:

- `Title`, `Brand`, `Model` üçün `[Required]`, `[MaxLength]`
- `Year` üçün `[Range]`
- `Price` üçün `[Range]`
- Email üçün `[EmailAddress]`

### 15. Layering qarışıqdır

Namespace-lər `Entry.*` altında toplanıb, amma fiziki layihələr `Entry`, `DataAccess`, `Business`, root API kimi ayrılıb. Root `wCarListing.csproj` ayrıca `Entry/**/*.cs`, `Business/**/*.cs`, `DataAccess/**/*.cs` fayllarını compile-dan remove edir, çünki onlar project reference kimi gəlir.

Bu işləyə bilər, amma adlandırma qarışıqlıq yaradır:

- `Entry` həm domain model, həm DTO projectidir
- `DataAccess` namespace-i `Entry.Data` kimi görünür
- Controller-lər root project-dədir, amma namespace `Entry.Controllers`-dir

Uzunmüddətli baxım üçün namespace-lər project adları ilə uyğunlaşdırılmalıdır.

### 16. Entity navigation collection-lar initialize olunmayıb

`Car.Images` və `Car.Features` null ola bilər. EF include etdikdə doldurur, amma yeni obyektlərdə və bəzi testlərdə null risklidir.

Məsələn belə olmalıdır:

```csharp
public ICollection<CarImage> Images { get; set; } = new List<CarImage>();
public ICollection<CarFeature> Features { get; set; } = new List<CarFeature>();
```

### 17. Tests yoxdur

Repo içində test project görünmür. Bu API üçün ən azı aşağıdakı testlər lazımdır:

- Register/login
- Admin login seed
- User yalnız öz elanını update/silə bilir
- Admin bütün elanları idarə edə bilir
- Image upload validation
- Reorder duplicate/missing id halları
- Block edilmiş user əməliyyat edə bilmir

## Yaxşı tərəflər

- API strukturu sadə və başa düşüləndir.
- JWT role claim düzgün istifadə olunur.
- Soft delete düşünülüb.
- Public listing endpoint-lərində `AsNoTracking()` istifadə olunub.
- Şəkil upload-larında size limit və extension whitelist var.
- MinIO-ya keçid üçün lokal image migration servisi yazılıb.
- `CreatedAt`, index-lər və relationship-lər əsasən düşünülüb.
- `ClaimsExtensions.GetUserId()` token claim parsing-i mərkəzləşdirir.

## Tövsiyə olunan düzəliş sırası

1. Build problemini sabitləşdirmək.
2. Admin seed password hashing səhvini düzəltmək.
3. Secret-ləri `appsettings.json`-dan çıxarmaq.
4. Migration zəncirini təmizləmək.
5. DTO validation-ları əlavə etmək.
6. Image magic bytes validation əlavə etmək.
7. Reorder duplicate/missing image id yoxlamasını düzəltmək.
8. MinIO bucket provisioning-i runtime upload path-indən çıxarmaq.
9. Entity collection-ları initialize etmək.
10. Integration/unit test project əlavə etmək.

## Ən kritik 5 fix

### Fix 1: Admin hash

`Program.cs` içində:

```csharp
PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!")
```

əvəzinə eyni sistem istifadə olunmalıdır:

```csharp
PasswordHash = Business.Concrete.PasswordService.Hash(adminPassword)
```

`adminPassword` environment variable-dan gəlməlidir.

### Fix 2: Register/Login null validation

Trim etməzdən əvvəl null yoxlanmalıdır:

```csharp
if (string.IsNullOrWhiteSpace(dto.Username) ||
    string.IsNullOrWhiteSpace(dto.Email) ||
    string.IsNullOrWhiteSpace(dto.Password))
    return BadRequest(...);

dto.Username = dto.Username.Trim();
dto.Email = dto.Email.Trim().ToLowerInvariant();
```

### Fix 3: Migration-ları düzəltmək

Yeni DB üçün migration-lar ardıcıl işləməlidir. `InitialMinioStorage` və `AddObjectKeyToCarImages` eyni kolonu iki dəfə yaratmamalıdır.

### Fix 4: Secret management

`appsettings.json`-dan bu məlumatlar çıxarılmalıdır:

- `Jwt:Key`
- `Minio:AccessKey`
- `Minio:SecretKey`
- production connection string

### Fix 5: Reorder validation

Göndərilən image ID-lər həm duplicate olmamalıdır, həm də car-dakı bütün şəkilləri əhatə etməlidir.

## Yoxlama nəticələri

İcra olunan komandalar:

```text
dotnet restore CarListing.sln
dotnet build CarListing.sln --no-restore
dotnet build wCarListing.csproj --no-restore -v:minimal
```

Nəticə:

- Restore uğurlu oldu.
- Build exit code `1` ilə uğursuz qayıtdı.
- Build output `0 error, 0 warning` göstərdiyi üçün problem çox güman lokal SDK/workload resolver vəziyyəti ilə bağlıdır.

## Nəticə

Layihənin əsas funksionallığı qurulub və direction düzgündür: auth, elan idarəsi, şəkil upload, admin panel backend-i və MinIO storage var. Amma hazırkı vəziyyətdə kodu production-a buraxmaq olmaz. İlk düzəldilməli yerlər security və build/migration sabitliyidir. Bu üçü düzələndən sonra validation və testlər əlavə olunmalıdır.
