# Backend Foundation Plan

Tarix: 2026-05-16

Bu plan `Compare Cars`, `Trust Score`, `AI Search Assistant`, `AI Match Score` ve `AI Price Advisor` feature-leri ucun backend temelini hazirlayir.

## Hazir olanlar

- Basic `Car` entity var: title, brand, model, year, price.
- Create/update/detail/list DTO-lari var.
- `GET /api/cars` pagination edir.
- Test project var ve esas baseline testler kecir.

## Umumi status

Hazirki backend foundation seviyesi: `90-95%`

Sebeb: elan modeli genislendi, DTO-lar update olundu, filter/search endpoint yazildi, migration yaradildi, metadata/compare/AI parser contract elave edildi ve unit + integration testler kecir. Qalan esas hisse frontend ile son field/value adlarini razilasmaq ve daha genis edge-case integration testleridir.

## Phase 1: Car modelini genisletmek

Status: `DONE`

### Kicik tasklar

- [x] `Car.Mileage` elave etmek.
- [x] `Car.FuelType` elave etmek.
- [x] `Car.BodyType` elave etmek.
- [x] `Car.Transmission` elave etmek.
- [x] `Car.Engine` elave etmek.
- [x] `Car.City` elave etmek.
- [x] `Car.Color` elave etmek.
- [x] `Car.Description` elave etmek.

### Niye lazimdir?

Bu field-ler olmadan filter, compare, trust score, match score ve price advisor deqiq islemeyecek. Meselen istifadeci "benzin, avtomat, 2019+" axtarirsa, backend-de bunlar struktur data kimi saxlanmalidir.

### Bizə nə qatır?

- Daha guclu filter.
- Daha yaxsi compare table.
- AI search ucun daha deqiq mapping.
- Price advisor ucun daha faydali oxsar elan tapmaq.

## Phase 2: DTO-lari update etmek

Status: `DONE`

### Kicik tasklar

- [x] `CreateCarDto` yeni field-leri qebul etsin.
- [x] `UpdateCarDto` yeni field-leri qebul etsin.
- [x] `CarDetailsDto` yeni field-leri qaytarsin.
- [x] `CarListItemDto` yeni field-leri qaytarsin.
- [x] Validation qaydalari elave edilsin.

### Niye lazimdir?

Entity-de field olmaq tek basina kifayet deyil. API request ve response-lar da bu melumatlari dasimalidir.

### Bizə nə qatır?

- Frontend form-lari daha dolu elan yarada bilir.
- Listing/detail sehifesi daha faydali melumat gosterir.
- Sonraki feature-ler eyni contract ustunde qurulur.

## Phase 3: Search/filter endpoint

Status: `DONE`

### Kicik tasklar

- [x] `CarSearchFilterDto` yaratmaq.
- [x] Brand/model/city/fuel/body/transmission filterleri.
- [x] Price min/max filterleri.
- [x] Year min/max filterleri.
- [x] Mileage max filteri.
- [x] Text search.
- [x] Sorting: newest, price asc/desc, year desc, mileage asc.
- [x] Pagination-i saxlayib response-u `PagedResultDto<CarListItemDto>` etmek.

### Niye lazimdir?

AI search-in ilk isi cumleni filter-lere cevirmekdir. Filter endpoint guclu deyilsə, AI parser-in neticesini isletmek olmur.

### Bizə nə qatır?

- Normal frontend filter UI.
- AI Search Assistant ucun hazir hedef.
- Compare/price/match feature-leri ucun daha deqiq car listeleri.

## Phase 4: Migration

Status: `DONE`

### Kicik tasklar

- [x] EF migration yaratmaq.
- [x] Snapshot yoxlamaq.
- [x] Build ile migration compile yoxlamaq.

### Niye lazimdir?

Kodda yeni field yaratmaq DB-ni avtomatik deyismir. Migration DB schema-ni kod modeli ile eynilesdirir.

### Bizə nə qatır?

- Yeni elan field-leri database-de saxlanilir.
- Lokal ve production DB-lere eyni schema tetbiq olunur.

## Phase 5: Tests

Status: `DONE`

### Kicik tasklar

- [x] DTO validation testleri genislensin.
- [x] Create/update mapping testleri elave edilsin.
- [x] Search/filter testleri elave edilsin.
- [x] Build ve test kecsin.

### Niye lazimdir?

Search/filter cox asan gizli bug yaradan yerdir. Test olmasa, bir filter basqasini poza biler.

### Bizə nə qatır?

- Yeni feature-ler ucun daha etibarli temel.
- Refactor edende qorxu azalir.
- Frontend paralel isleyende backend davranisi stabil qalir.

## Yekun yoxlama

- `dotnet test Tests\CarListing.Tests\CarListing.Tests.csproj -v:minimal` ugurla kecdi: 23 passed, 0 failed.
- `.\build.cmd -v:minimal` ugurla kecdi: 0 warning, 0 error.

## Qalan polish tasklar

- [x] Integration/API testleri ile real HTTP flow yoxlamaq.
- [x] Frontend ucun field adlari ve value listlerini backend metadata endpoint kimi vermek.
- [x] AI Search Assistant ucun parser contract-i yazmaq.
- [x] Compare Cars endpoint-ine baslamaq.

## Elave edilen polish isleri

### Metadata endpoint

`GET /api/cars/metadata`

Frontend bu endpoint-den fuel type, body type, transmission ve sort option listlerini ala biler.

Niye lazimdir?

- Frontend hardcoded list saxlamir.
- Backend ve frontend eyni value-larla isleyir.
- Sonra value elave etmek daha rahat olur.

### Compare Cars endpoint

`GET /api/cars/compare?ids=1&ids=2`

2-4 aktiv elani muqayise ucun qaytarir.

Niye lazimdir?

- Compare Cars feature-in backend baslangicidir.
- Frontend yan-yana table qura biler.
- User secim ederken qiymet, il, yurush, engine, fuel, body kimi sahələri bir yerde gorur.

### AI Search parser contract

`POST /api/cars/ai-search/parse`

Hazirda rule-based MVP parser-dir. Sorğunu `CarSearchFilterDto`-ya cevirmeye baslayir.

Niye lazimdir?

- AI Search Assistant ucun API contract indiden sabitlenir.
- Sonra real AI parser elave edilende frontend contract deyismir.
- Parser-in neticesi movcud `/api/cars` filter endpoint-i ile islene bilir.

### Integration testler

`WebApplicationFactory` ile real HTTP flow testleri elave edildi:

- register token qaytarir
- `/api/cars` filter HTTP uzerinden isleyir
- compare endpoint yanlis inputa `400 BadRequest` qaytarir

Niye lazimdir?

- Unit testlerden elave olaraq routing, model binding, DI ve real JSON response yoxlanir.
- Frontend baslayanda API contract-a daha cox guvenmek olur.

## Son yoxlama

- `dotnet test Tests\CarListing.Tests\CarListing.Tests.csproj -v:minimal` ugurla kecdi: 30 passed, 0 failed.
- `.\build.cmd -v:minimal` ugurla kecdi: 0 warning, 0 error.
