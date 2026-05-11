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

Status: `DONE WITH WORKAROUND`

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

`dotnet build CarListing.sln` normal parallel rejimde hele de `0 error, 0 warning` gostermesine baxmayaraq exit code `1` qaytarir. `dotnet build CarListing.sln /m:1` ugurla kecir.

Bu o demekdir ki, kod compile olunur; problem kod sintaksisinde deyil. Problem lokal MSBuild parallel execution/workload resolver davranisi ile baglidir.

Hazirda stabil build komandasi:

```powershell
.\build.cmd
```

ve ya:

```powershell
dotnet build CarListing.sln /m:1
```

Son yoxlama neticesi:

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
- EF migration list artiq yeni migration-i gosterir:
  `20260507175358_InitialCreate`
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

### Ne edirik

Request DTO-larina `DataAnnotations` elave etdik ki, `[ApiController]` invalid payload-lari action-a girmeden avtomatik saxlasin.

### Edilen deyisiklikler

- `RegisterDto` ucun `Required`, `StringLength`, `EmailAddress` elave edildi.
- `LoginDto` ucun `Required`, `EmailAddress` elave edildi.
- `CreateCarDto` ve `UpdateCarDto` ucun `Required`, `StringLength`, `Range` elave edildi.
- `CreateCarFeatureDto` ucun `Required` ve `StringLength` elave edildi.
- `AuthController` icinde `Trim()` null riski yaradan manual check bloklari cixarildi, email normalization `ToLowerInvariant()` ile saxlanildi.
- `CarsController` icinde create/update ucun tekrar yazilan string/year/price manual validation-lari cixarildi.

### Niye edirik

Evvel `Trim()` validation-dan once cagrildigi ucun null request-lerde exception riski var idi. Indi bu yoxlama framework seviyyesinde aparilir ve controller daha temiz qalir.

### Yoxlama neticesi

`.\build.cmd -v:minimal` ugurla kecdi.

## Problem 6: Image upload validation zeifdir

Status: `TODO`

### Kicik tasklar

- [ ] ContentType ve extension yoxlamasini saxlamaq.
- [ ] Magic bytes yoxlamasi elave etmek.
- [ ] JPG/PNG/WebP/AVIF ucun minimal header yoxlamasi yazmaq.
- [ ] Test upload hallari yazmaq.

## Problem 7: Reorder duplicate id yoxlamir

Status: `TODO`

### Kicik tasklar

- [ ] Duplicate image id halini yoxlamaq.
- [ ] Missing image id halini yoxlamaq.
- [ ] Set beraberliyi validation-i elave etmek.
- [ ] Endpoint-i yeniden yoxlamaq.

## Problem 8: MinIO bucket policy her emeliyyatda set olunur

Status: `TODO`

### Kicik tasklar

- [ ] `EnsureBucketAsync` davranisini ayirmaq.
- [ ] Startup/provisioning ucun daha uygun yer secmek.
- [ ] Upload/delete path-ini yunguletmek.

## Problem 9: Entity collection-lar initialize olunmayib

Status: `TODO`

### Kicik tasklar

- [ ] `Car.Images` ucun default `new List<CarImage>()` vermek.
- [ ] `Car.Features` ucun default `new List<CarFeature>()` vermek.
- [ ] Nullable warning-lere baxmaq.

## Problem 10: Test project yoxdur

Status: `TODO`

### Kicik tasklar

- [ ] Test framework secmek.
- [ ] Minimum auth testleri.
- [ ] Minimum car authorization testleri.
- [ ] Image validation testleri.

### Senden lazim ola bilecek melumat

Test ucun xUnit, NUnit, yoxsa MSTest isteyirsen?
