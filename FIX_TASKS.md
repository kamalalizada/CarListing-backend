# CarListing Fix Tasks

Tarix: 2026-05-01

Bu faylda `CODE_ANALYSIS.md`-de tapilan problemleri bir-bir, kicik tasklara boluruk. Her problem ucun:

- ne edirik
- niye edirik
- ne istifade edirik
- status
- yoxlama neticesi

## Status izahi
- `TODO`: hele baslanmayib
- `IN PROGRESS`: hazirda uzerinde islenir
- `DONE`: duzeldilib ve yoxlanib
- `BLOCKED`: senden melumat ve ya icaze lazimdir
## Problem 1: Build ugursuz qayidir

Status: `DONE`

### Problem

`dotnet build CarListing.sln` ve `dotnet build wCarListing.csproj` parallel MSBuild ile exit code `1` qaytarirdi, amma output-da `0 error, 0 warning` gorunurdu.

### Ne ucun vacibdir

Build stabil deyilse:

- CI/CD fail ede biler
- komanda uzvlerinde ferqli netice cixa biler
- real kod sehvleri gizlene biler
- deployment riskli olar

### Ne yoxlandi

- `dotnet --info`
- `dotnet workload list`
- `dotnet restore wCarListing.csproj`
- `dotnet build Entry/Entry.csproj`
- `dotnet build DataAccess/DataAccess.csproj`
- `dotnet build Business/Business.csproj`
- `dotnet build wCarListing.csproj`
- `dotnet build CarListing.sln /m:1`

### Tapilan sebeb

Tek-tek project-ler ugurla build olunur. Solution ve root API project normal parallel MSBuild ile fail edir. `/m:1` ile build ugurludur. Bu, kod compile sehvinden cox project dependency sirasi / MSBuild parallel build problemi kimi gorunur.

### Edilecek kicik tasklar

- [x] Project-leri tek-tek build edib kod compile sehvini yoxlamaq.
- [x] Solution-u `/m:1` ile build edib parallel build hipotezini yoxlamaq.
- [x] `CarListing.sln` icinde project dependency siralarini aciq gostermek.
- [x] Root API project-e `Entry/Entry.csproj` birbasa reference elave etmek.
- [x] `dotnet workload repair` yoxlamaq.
- [x] `dotnet workload update` ile Aspire workload-u yenilemek.
- [x] `dotnet build CarListing.sln` ile yeniden yoxlamaq.
- [x] Stabil build ucun `build.cmd` helper yaratmaq.
- [x] Neticeni bu faylda qeyd etmek.

### Ne istifade edirik

- .NET CLI
- MSBuild solution dependency metadata
- `build.cmd` helper script

### Edilen deyisiklikler

- `CarListing.sln` icinde project dependency sirasi aciq yazildi.
- `wCarListing.csproj` icine `Entry/Entry.csproj` birbasa `ProjectReference` kimi elave edildi.
- Evvel `build.ps1` yoxlandi, amma Windows PowerShell execution policy script-i blokladi.
- `build.cmd` yaradildi ve build-in `/m:1` ile stabil getmesi ucun qisa helper yazildi.
- Lokal SDK-da `dotnet workload repair` yoxlandi.
- `dotnet workload update` edildi, Aspire workload `8.2.2/8.0.100` oldu.

### Yekun netice

Evvel `dotnet build CarListing.sln` normal parallel rejimde `0 error, 0 warning` gostermesine baxmayaraq exit code `1` qaytarirdi. Workload repair/update ve project dependency/reference duzelislerinden sonra normal solution build yeniden yoxlanildi ve ugurlu kecdi.

Bu o demekdir ki, kod compile olunur ve hazirki lokal muhitde parallel solution build de stabil gorunur.

Stabil build komandasi:

```powershell
.\build.cmd
```

ve ya:

```powershell
dotnet build CarListing.sln /m:1
```

Son yoxlama neticesi:

- `dotnet build CarListing.sln -v:minimal` ugurlu oldu.
- `dotnet build CarListing.sln /m:1 -v:minimal` ugurlu oldu.
- `dotnet build wCarListing.csproj --no-restore /m:1 -v:minimal` ugurlu oldu.
- NU1900 warning gorundu, cunki NuGet vulnerability audit `https://api.nuget.org/v3/index.json` sorgusunu ala bilmedi. Bu compile error deyil.

## Problem 2: Default admin parolu ve hash sistemi qarisiqdir

Status: `DONE`

### Problem

Admin user `Program.cs` icinde BCrypt ile seed olunur, amma login PBKDF2 formatini yoxlayan `PasswordService.Verify` istifade edir. Buna gore default admin login islemiye biler.

### Kicik tasklar

- [x] Admin seed kodunu oxumaq.
- [x] `PasswordService.Hash` ile eyni hash sistemine kecirmek.
- [x] Admin parolunu hardcoded saxlamaq evezine configuration/env-den oxumaq.
- [x] Movcud admin BCrypt/legacy hash ile qalibsa, onu yeni PBKDF2 formata kecirmek.
- [x] Build ile yoxlamaq.

### Ne edirik

Default admin artiq kodun icinde `"Admin123!"` kimi hardcoded deyil. `Program.cs` `Admin:Password` config deyerini oxuyur. Bu deyer production-da environment variable ile verile biler:

```powershell
$env:Admin__Password = "strong-real-password"
```

Development ucun `appsettings.json` icinde placeholder saxlanildi:

```json
"Admin": {
  "Password": "CHANGE_ME_ONLY_FOR_DEVELOPMENT"
}
```

### Niye edirik

Evvel admin BCrypt ile hash olunurdu, amma login PBKDF2 yoxlayan `PasswordService.Verify` istifade edirdi. Bu format uygunsuzlugu admin login-i poza bilerdi. Indi admin de register olunan normal user-ler kimi `PasswordService.Hash` ile hash olunur.

Eger database-de admin artiq evvelden BCrypt hash ile yaranibsa, `Program.cs` startup zamani hash formatini yoxlayir. Hash PBKDF2 formatinda deyilse, `Admin:Password` deyerinden yeni hash yaradib admini yenileyir.

### Ne istifade edirik

- ASP.NET Core configuration sistemi
- Environment variable mapping: `Admin__Password` -> `Admin:Password`
- Movcud `Business.Concrete.PasswordService`

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi.

## Problem 3: Secret-ler appsettings.json icindedir

Status: `DONE`

### Problem

JWT key ve MinIO credential-leri repo configinde aciq yazilib.

### Kicik tasklar

- [x] Production secret-leri `appsettings.json`-dan cixarmaq.
- [x] Development ucun numune config saxlamaq.
- [x] Environment variable adlarini qeyd etmek.
- [x] Placeholder secret-ler ucun startup guard elave etmek.
- [x] Build ile yoxlamaq.

### Ne edirik

`appsettings.json` icinde real secret kimi gorunen deyerleri placeholder etdik. Real deyerler environment variable ve ya user-secrets ile verilmelidir.

### Niye edirik

Secret-ler repo icinde qalsa:

- Git tarixcesinde qalir
- Production credential sizdirila biler
- Butun developerlerde eyni JWT/MinIO secret islenir
- Deploy muhitlerinde config idare etmek cetinlesir

### Ne istifade edirik

- ASP.NET Core configuration sistemi
- Environment variable mapping
- Startup validation guard

### Environment variable adlari

PowerShell ucun:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=CarListing;Trusted_Connection=True;"
$env:Jwt__Key = "strong-jwt-secret-at-least-32-chars"
$env:Jwt__Issuer = "CarListingApi"
$env:Admin__Password = "strong-admin-password"
$env:Minio__Endpoint = "localhost:9000"
$env:Minio__AccessKey = "real-minio-access-key"
$env:Minio__SecretKey = "real-minio-secret-key"
$env:Minio__BucketName = "car-images"
$env:Minio__PublicBaseUrl = "http://localhost:9000/car-images"
$env:Minio__UseSsl = "false"
```

### Edilen deyisiklikler

- `appsettings.json` icinde `Jwt:Key`, `Minio:AccessKey`, `Minio:SecretKey` placeholder edildi.
- `Program.cs` `Jwt:Key` ucun `CHANGE_ME` ve `SUPER_SECRET` kimi placeholder-leri bloklayir.
- `MinioImageStorageService` `Minio:AccessKey` ve `Minio:SecretKey` placeholder-lerini bloklayir.

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi. NU1900 warning compile error deyil, NuGet audit network sorgusu ile baglidir.

## Problem 4: Migration-lar qarisiqdir

Status: `DONE`

### Problem

`InitialMinioStorage` ve `AddObjectKeyToCarImages` migration-lari eyni kolonu tekrar idare edir kimi gorunur.

### Kicik tasklar

- [x] Migration zencirini yoxlamaq.
- [x] Yeni temiz initial migration-i EF CLI ile yaratmaq.
- [x] Duplicate migration-lari silmek.
- [x] Runtime schema patch-i (`EnsureMinioImageColumnsAsync`) silmek.
- [x] EF design-time factory elave etmek.
- [x] Migration list ve script generation yoxlamaq.
- [x] Build ile yoxlamaq.

### Ne edirik

Kohne qarisiq migration zenciri silindi. Manual migration yaratmaq duzgun yanasma olmadigi ucun migration faylini repo-dan cixardiq. Yeni migration EF CLI ile yaradildi.

Yeni migration indiki modelin tam DB strukturunu yaratmalidir:

- `Users`
- `Cars`
- `CarFeatures`
- `CarImages`
- index-ler
- foreign key-ler
- `CarImages.ObjectKey`
- `CarImages.ImageUrl nvarchar(500)`

### Niye edirik

Evvel iki migration bir-biri ile toqqusma riski yaradir:

- `InitialMinioStorage` artiq `ObjectKey` yaradir.
- `AddObjectKeyToCarImages` yeniden `ObjectKey` elave etmeye calisir.

Bu yeni DB qurulanda migration fail ede bilerdi. Runtime-da `EnsureMinioImageColumnsAsync` ile SQL patch etmek de duzgun migration idaresi deyil.

### Edilen deyisiklikler

- `20260427160000_InitialMinioStorage.cs` silindi.
- `20260427182000_AddObjectKeyToCarImages.cs` silindi.
- Manual yaradilan `20260502000000_InitialCreate.cs` silindi.
- Manual snapshot `AppDbContextModelSnapshot.cs` silindi.
- EF CLI ile yeni migration yaradildi:
  `DataAccess/Migrations/20260507175358_InitialCreate.cs`
- EF CLI ile yeni snapshot yaradildi:
  `DataAccess/Migrations/AppDbContextModelSnapshot.cs`
- `Program.cs` icinden `EnsureMinioImageColumnsAsync` ve onun `ExecuteSqlRawAsync` patch-leri silindi.
- `DataAccess/AppDbContextFactory.cs` elave edildi ki, EF CLI app startup secret guard-larina bagli qalmasin.
- `Microsoft.EntityFrameworkCore.Design` package-i design-time migration tooling ucun elave edildi.

### Ne istifade edirik

- EF Core migration
- `IDesignTimeDbContextFactory<AppDbContext>`
- `dotnet ef migrations list`
- `dotnet ef migrations script`

### Icra olunan komandalar

Migration yaratmaq ucun bu env deyerleri ile isledik:

```powershell
$env:Jwt__Key = "strong-jwt-secret-at-least-32-chars"
$env:Admin__Password = "strong-admin-password"
$env:Minio__AccessKey = "real-minio-access-key"
$env:Minio__SecretKey = "real-minio-secret-key"
dotnet ef migrations add InitialCreate --project DataAccess\DataAccess.csproj --startup-project wCarListing.csproj
dotnet ef migrations list --project DataAccess\DataAccess.csproj --startup-project wCarListing.csproj
dotnet ef migrations script --project DataAccess\DataAccess.csproj --startup-project wCarListing.csproj
.\build.cmd -v:minimal
```

### Yoxlama neticesi

- Build ugurlu kecdi.
- EF migration list artiq yeni migration-i gosterdi.
- EF script generation ugurlu oldu ve SQL script tam yarandi.
- `sqllocaldb start MSSQLLocalDB` ugurlu oldu.
- `dotnet ef database drop` ve `dotnet ef database update` ugurlu oldu.

Movcud local DB varsa ve LocalDB/SQL instance saglamdirsa, reset etmek ucun:

```powershell
dotnet ef database drop --force --project DataAccess\DataAccess.csproj --startup-project wCarListing.csproj
dotnet ef database update --project DataAccess\DataAccess.csproj --startup-project wCarListing.csproj
```

## Problem 5: DTO validation zeifdir

Status: `DONE`

### Kicik tasklar

- [x] Register/Login null yoxlamasini duzeltmek.
- [x] Car create/update validation-larini DataAnnotations ile guclendirmek.
- [x] Controller daxilindeki tekrar validation kodunu azaltmaq.
- [x] Build ile yoxlamaq.

### Problem

Validation hissesi controller-lerin icinde el ile yazildigi ucun hem tekrar var idi, hem de `Trim()` kimi emeliyyatlar null request geldikde exception riski yarada bilirdi.

### Ne edirik

Validation-u DTO seviyyesine dasiyiriq. `[ApiController]` ve `DataAnnotations` birlikde request action-a girmeden once modelin dogrulugunu yoxlayir.

### Edilen deyisiklikler

- `RegisterDto` ucun `Required`, `StringLength`, `EmailAddress` elave edildi.
- `LoginDto` ucun `Required` ve `EmailAddress` elave edildi.
- `CreateCarDto` ve `UpdateCarDto` ucun `Required`, `StringLength`, `Range` elave edildi.
- Decimal `Range` limitleri culture-dan asili parse olunmasin deye `ParseLimitsInInvariantCulture = true` elave edildi.
- `CreateCarFeatureDto` ucun `Required` ve `StringLength` elave edildi.
- `AuthController` icinde null riski yaradan manual block cixarildi ve normalization saxlanildi.
- `CarsController` icinde create/update ucun tekrar yazilan manual validation bloklari cixarildi.

### Niye edirik

Bu yanasma controller-i sade saxlayir, validation qaydalarini merkezi edir ve invalid request-ler ucun daha stabil `400 Bad Request` davranisi verir.

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi.

## Problem 6: Image upload validation zeifdir

Status: `DONE`

### Kicik tasklar

- [x] ContentType ve extension yoxlamasini saxlamaq.
- [x] Magic bytes yoxlamasi elave etmek.
- [x] JPG/PNG/WebP/AVIF ucun minimal header yoxlamasi yazmaq.
- [x] Build ile yoxlamaq.

### Problem

Upload endpoint-i evvel yalniz bunlara baxirdi:

- `ContentType`
- fayl extension-u
- size limit

Amma `ContentType` client terefinden gonderilir ve saxtalasdirila biler. Yani `.jpg` adli, amma eslinde shekil olmayan bir fayl gondermek mumkundur.

### Ne edirik

Validation-u bir yerde toplayiriq ve faylin uc seviyede dogrulugunu yoxlayiriq:

- extension icaze verilmis formatlardan olmalidir
- MIME type icaze verilmis formatlardan olmalidir
- faylin ilk byte-lari heqiqi format imzasina uygun olmalidir

### Edilen deyisiklikler

- `Services/ImageFileValidator.cs` elave edildi.
- JPG, PNG, WebP ve AVIF ucun minimal signature yoxlamasi yazildi.
- `CarsController.UploadImages` icinde upload-dan once:
  - content type yoxlanir
  - extension yoxlanir
  - stream signature yoxlanir
- Validation qaydalari controller daxilinde sepelik halda yox, helper icinde merkezilesdirildi.

### Niye edirik

Bu yanasma “yalniz image kimi gorunen fayl” yox, “heqiqeten image olan fayl” qebul etmek ucun daha gucludur. Eyni zamanda upload endpoint-indeki qaydalari bir yerde topladigi ucun baximi da rahatlasdirir.

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi.

## Problem 7: Reorder duplicate id yoxlamir

Status: `DONE`

### Kicik tasklar

- [x] Duplicate image id halini yoxlamaq.
- [x] Missing image id halini yoxlamaq.
- [x] Set beraberliyi validation-i elave etmek.
- [x] Build ile yoxlamaq.

### Problem

`ReorderImages` endpoint-i sekil ID-lerinin yeni sirasini qebul edir. Evvel yalniz gonderilen ID-lerin hemin elana aid olub-olmadigi yoxlanirdi.

Amma iki risk qalirdi:

- eyni image ID iki defe gonderile bilerdi
- car-daki hansisa image ID siyahida olmaya bilerdi

Bu halda sira natamam ve ya sehv hesablana bilerdi.

### Ne edirik

Gonderilen ID siyahisini set-e ceviririk ve iki seyi yoxlayiriq:

- set sayi original list sayina beraber olmalidir, eks halda duplicate var
- gonderilen ID set-i car-daki image ID set-i ile tam beraber olmalidir

Yalniz bu iki yoxlama kecenden sonra `Order` deyeri yenilenir.

### Edilen deyisiklikler

- `CarsController.ReorderImages` icinde duplicate ID yoxlamasi elave edildi.
- Missing ve extra ID hallari ucun set beraberliyi yoxlamasi elave edildi.
- `First(...)` tekrar axtarisi yerine `ToDictionary(...)` ile image lookup yazildi.

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi.

## Problem 8: MinIO bucket policy her emeliyyatda set olunur

Status: `DONE`

### Kicik tasklar

- [x] `EnsureBucketAsync` davranisini ayirmaq.
- [x] Startup/provisioning ucun daha uygun yer secmek.
- [x] Upload/delete path-ini yunguletmek.

### Problem

`MinioImageStorageService` evvel her upload ve delete emeliyyatinda bucket-in olub-olmadigini yoxlayirdi ve public read policy-ni yeniden set edirdi.

### Ne edirik

Bucket yaradilisini ve policy tetbiqini image storage provisioning addimi kimi ayirdiq. `UploadAsync` ve `DeleteAsync` yalniz oz esas isini gorur:

- upload fayli MinIO-ya yazir
- delete object-i MinIO-dan silir

Provisioning ise app startup zamani bir defe icra olunur.

### Edilen deyisiklikler

- `Services/IImageStorageProvisioner.cs` elave edildi.
- `MinioImageStorageService` hem `IImageStorageService`, hem de `IImageStorageProvisioner` implement edir.
- `EnsureBucketAsync` public `ProvisionAsync` davranisina cevrildi.
- `UploadAsync` ve `DeleteAsync` icinden bucket/policy yoxlamasi cixarildi.
- `Program.cs` icinde startup zamani `IImageStorageProvisioner.ProvisionAsync()` cagrilir.

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi.

## Problem 9: Entity collection-lar initialize olunmayib

Status: `DONE`

### Kicik tasklar

- [x] `Car.Images` ucun default `new List<CarImage>()` vermek.
- [x] `Car.Features` ucun default `new List<CarFeature>()` vermek.
- [x] Entity navigation ve string property-lari nullable baximindan səliqəyə salmaq.
- [x] Build ile yoxlamaq.

### Problem

Entity modeller EF terefinden sonradan doldurulan property-lere malik idi, amma C# nullable analizine gore bunlar konstruktor cixanda bos qalirdilar. Bu da `CS8618` warning-lere sebeb olurdu.

### Ne edirik

Modelin runtime davranisini deyismeden, initialization niyyetini aciq yaziriq:

- string property-ler `= null!;`
- reference navigation property-ler `= null!;`
- collection navigation property-ler `= new List<...>();`

### Edilen deyisiklikler

- `Car.Images` default olaraq `new List<CarImage>()` ile initialize edildi.
- `Car.Features` default olaraq `new List<CarFeature>()` ile initialize edildi.
- `Car.User`, `CarImage.Car`, `CarFeature.Car` navigation property-leri nullable warning vermeyecek formada isare olundu.
- `Car`, `CarImage`, `CarFeature` daxilindeki required string property-ler aciq sekilde initialize edildi.

### Niye edirik

Bu yanasma iki seyi birlikde verir:

- EF modeli oldugu kimi qalir
- compile vaxti gereksiz nullable warning-ler yox olur

Collection-larin bos gelmesi ehtimali de aradan qalxir, buna gore kodun qalan hissesinde `Images` ve `Features` ile islemek daha tehlukesiz olur.

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi.

## Problem 10: Test project yoxdur

Status: `DONE`

### Kicik tasklar

- [x] Test framework secmek.
- [x] Minimum auth/password testleri.
- [x] Minimum car authorization testleri.
- [x] Image validation testleri.

### Ne edirik

xUnit test project elave edildi ve solution-a qosuldu. Ilk test seti daha cox riskli, amma DB/HTTP dependency teleb etmeyen hisseleri yoxlayir:

- password hash/verify davranisi
- DTO validation qaydalari
- non-owner user-in car update/delete emeliyyatlarinda `Forbid` almasi
- image extension/content-type/signature validation-i

### Edilen deyisiklikler

- `Tests/CarListing.Tests/CarListing.Tests.csproj` yaradildi.
- Test project `CarListing.sln` icine elave edildi.
- `PasswordServiceTests` elave edildi.
- `ImageFileValidatorTests` elave edildi.
- `DtoValidationTests` elave edildi.
- `CarAuthorizationTests` elave edildi.
- `wCarListing.csproj` icinde `Tests/**/*.cs` root web project compile-indan cixarildi.
- Test neticesinde tapilan decimal `Range` culture problemi `CreateCarDto` ve `UpdateCarDto` ucun duzeldildi.

### Ne istifade edirik

- xUnit
- EF Core InMemory
- Microsoft.NET.Test.Sdk
- DataAnnotations validation

### Yoxlama neticesi

`dotnet test Tests\CarListing.Tests\CarListing.Tests.csproj -v:minimal` ugurla kecdi:

- 14 test passed
- 0 failed

`.\build.cmd -v:minimal` ugurla kecdi.


---

# Next hardening tasks

Bu bolme `CODE_ANALYSIS.md`-de qalan production risklerini yeni, izlene bilen tasklara cevirir. Ilk 10 problem compile/build/security baseline ucun baglanib. Asagidaki tasklar ise production davranisi, maintenance ve daha genis test coverage ucundur.

## Problem 11: Public bucket security qerari netlesmeyib

Status: `DONE`

### Problem

MinIO bucket policy-si butun `car-images` bucket-i ucun public `s3:GetObject` icazesi verir. Hazirki elan sekilleri public ola biler, amma gelecekde private/user-sensitive upload-lar eyni storage yanaşmasina dusse risk yaranar.

### Ne edirik

Public access qaydasini explicit config ve documentation ile idare etmek lazimdir:

- car image bucket-in public olub-olmadigini config-de aciq gostermek
- public bucket policy-ni yalniz public image use-case ucun tetbiq etmek
- gelecek private file-lar ucun presigned URL yanaşmasini ayri saxlamaq

### Kicik tasklar

- [x] `Minio:MakeBucketPublic` kimi config flag elave etmek.
- [x] `ProvisionAsync` policy-ni yalniz flag true olanda set etsin.
- [x] Default development davranisini secmek.
- [x] Private upload-lar ucun presigned URL yanaşmasini note kimi qeyd etmek.
- [x] Build/test ile yoxlamaq.

### Edilen deyisiklikler

- `MinioOptions.MakeBucketPublic` elave edildi.
- `appsettings.json` development default olaraq `"MakeBucketPublic": true` saxlayir.
- Production-da public policy istemirikse, `Minio__MakeBucketPublic=false` verilmelidir.
- `MinioImageStorageService.ProvisionAsync` bucket-i yaradandan sonra policy-ni yalniz `MakeBucketPublic == true` olanda set edir.
- Private upload-lar gelecekde eyni public bucket policy-sine baglanmamali, presigned URL ve ya ayri private bucket yanaşmasi ile hell edilmelidir.

### Yoxlama neticesi

- `.\build.cmd -v:minimal` ugurla kecdi.
- `dotnet test Tests\CarListing.Tests\CarListing.Tests.csproj -v:minimal` ugurla kecdi: 14 passed, 0 failed.

## Problem 12: Soft delete sekilleri storage-dan silmir

Status: `DONE`

### Problem

Elan silinende `IsActive = false` edilir, amma MinIO-daki sekiller qalir. Bu biznes baximindan duzgun ola biler, amma storage cost, cleanup ve privacy telebleri ucun riskdir.

### Ne edirik

Soft delete davranisini explicit qerar kimi ayirmaq lazimdir:

- soft delete zamani sekilleri saxlamaq
- ve ya purge/cleanup mexanizmi ile sonradan silmek

### Kicik tasklar

- [x] Soft delete zamani image-larin saxlanmasi biznes qerari kimi qeyd edilsin.
- [x] Admin/maintenance cleanup servisi ucun yanaşma secilsin.
- [x] Deactivated car image-larini temizleyen service yazilsin ve ya task kimi ayrilsin.
- [x] Storage delete failure halinda DB consistency davranisi netlesdirilsin.
- [x] Test senarisi elave edilsin.

### Edilen deyisiklikler

- Soft delete davranisi oldugu kimi saxlanildi: elan silinende `IsActive = false` olur.
- Storage temizliyi ucun ayri `InactiveCarImageCleanupService` elave edildi.
- Service yalniz inactive car-lara aid image-lari storage-dan silir ve sonra DB row-larini temizleyir.
- Storage delete fail ederse exception qalxir ve DB row-lari silinmir. Bu, DB-nin storage-dan evvel temizlenib orphan risk yaratmasinin qarsisini alir.
- Service DI-a `Scoped` kimi elave edildi ki, sonra admin/maintenance endpoint, CLI flag ve ya background job terefinden cagrila bilsin.
- `InactiveCarImageCleanupServiceTests` elave edildi.

### Yoxlama neticesi

- `dotnet test Tests\CarListing.Tests\CarListing.Tests.csproj -v:minimal` ugurla kecdi: 16 passed, 0 failed.
- `.\build.cmd -v:minimal` ugurla kecdi.

## Problem 13: Block edilmis user-in movcud tokenleri aktiv qala biler

Status: `DONE`

### Problem

Login zamani blocked user yoxlanir ve car endpoint-lerinde `IsCurrentUserBlocked()` var. Amma bu yoxlama her yeni protected endpoint-de unudula biler. JWT token oz lifetime-i bitene qeder formal olaraq valid qalir.

### Ne edirik

Blocked user yoxlamasini merkezi auth layer-e dasimaq lazimdir:

- JWT validation event-de user statusunu DB-den yoxlamaq
- ve ya custom authorization policy/handler yazmaq

### Kicik tasklar

- [x] `JwtBearerEvents.OnTokenValidated` ile user blocked yoxlamasini araşdırmaq.
- [x] DB query cost ve caching yanaşmasini secmek.
- [x] Controller-lerde tekrar `IsCurrentUserBlocked()` ehtiyacini azaltmaq.
- [x] Blocked user token testleri elave etmek.
- [x] Build/test ile yoxlamaq.

### Edilen deyisiklikler

- `BlockedUserTokenValidator` elave edildi.
- JWT auth config-inde `JwtBearerEvents.OnTokenValidated` elave edildi.
- Token validasiya olunandan sonra user id DB-den yoxlanir; user blocked-dursa `context.Fail("User is blocked.")` edilir.
- Controller-lerdeki tekrar `IsCurrentUserBlocked()` bloklari cixarildi.
- Bu yoxlama her protected endpoint ucun merkezi auth pipeline-da isleyir.
- `BlockedUserTokenValidatorTests` elave edildi.

### Qeyd

Bu yanasma her authenticated request-de yüngül DB query edir. Traffic artanda bunu qisa TTL cache ile optimallasdirmaq olar, amma indiki merhelede correctness daha vacibdir.

### Yoxlama neticesi

- `dotnet test Tests\CarListing.Tests\CarListing.Tests.csproj -v:minimal` ugurla kecdi: 19 passed, 0 failed.
- `.\build.cmd -v:minimal` ugurla kecdi.

## Problem 14: Namespace/layering qarisiqligi qalir

Status: `TODO`

### Problem

Project adlari ve namespace-ler tam uygun deyil:

- `Entry` hem domain model, hem DTO projectidir
- `DataAccess` namespace-i `Entry.Data` kimi gorunur
- controller namespace-i `Entry.Controllers`-dir, amma controller-ler root web project-dedir

Bu hal hazirda build-i pozmur, amma uzunmuddetli maintenance ucun qarisiqliq yaradir.

### Ne edirik

Namespace ve project serhedlerini refactor planina salmaq lazimdir. Bu task boyuk blast radius-a malik oldugu ucun bir defe-de kor refactor etmek yox, merheleli aparmaq daha saglamdir.

### Kicik tasklar

- [ ] Hedef namespace sxemini secmek (`CarListing.Api`, `CarListing.Domain`, `CarListing.DataAccess`, `CarListing.Business`).
- [ ] Project rename lazimdirmi, yoxsa yalniz namespace rename kifayetdirmi qerar vermek.
- [ ] Refactor planini ve riskleri yazmaq.
- [ ] Namespace rename-i merheleli etmek.
- [ ] Build/test ile yoxlamaq.

## Problem 15: Integration/API test coverage hele minimaldir

Status: `TODO`

### Problem

Test project var ve ilk 14 test kecir. Amma tam API davranisi ucun hele integration testler yoxdur:

- register/login HTTP flow
- admin seed/login
- image upload endpoint-i
- reorder duplicate/missing id HTTP response-lari
- blocked user-in protected endpoint-lere gire bilmemesi

### Ne edirik

`WebApplicationFactory` esasli integration test qatini elave etmek lazimdir. DB ucun test environment ayri olmalidir ki, local development DB-ye toxunulmasin.

### Kicik tasklar

- [ ] `Microsoft.AspNetCore.Mvc.Testing` paketi elave etmek.
- [ ] Test app factory yazmaq.
- [ ] Test config ile secret guard-lari kecmek.
- [ ] Test DB strategiyasini secmek: SQLite in-memory ve ya SQL Server LocalDB test database.
- [ ] Auth register/login integration testleri yazmaq.
- [ ] Car authorization integration testleri yazmaq.
- [ ] Image upload/reorder integration testleri yazmaq.
- [ ] Build/test ile yoxlamaq.
