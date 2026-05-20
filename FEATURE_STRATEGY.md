# CarListing Feature Strategy

Tarix: 2026-05-15

## Meqsed

Bu sened CarListing layihəsini sade elan saytından daha ağıllı, etibarlı və istifadəsi rahat avtomobil platformasına çevirmək üçün feature istiqamətlərini toplayır.

Əsas məqsəd:

- Turbo tipli klassik marketplace-dən fərqlənmək
- alıcı üçün qərar verməyi asanlaşdırmaq
- satıcı üçün elan yerləşdirməyi və satışı sürətləndirmək
- platformaya etibar qatmaq

## Əsas strategiya

Sadə filter + elan sistemi artıq bazarda var. Fərqi yaradan şey bunlardır:

- AI ilə niyyət əsaslı axtarış
- qiymət və etibar analizi
- müqayisə və qərar dəstəyi
- satıcıya köməkçi alətlər
- yoxlanmış və daha şəffaf elanlar

Yəni məhsulun gələcək formulu belədir:

```text
Listing platform + AI assistant + trust layer + decision tools
```

## Sənin AI ideyan

### Feature: AI Search Assistant

İstifadəçi klassik filter seçmək əvəzinə yazır:

- `mene 20 minlik toyota goster`
- `2019-dan yuxari olsun, ekonom olsun, aile ucun rahat olsun`
- `birinci masin ucun sedan axtariram`
- `20-25 min arasi, servis xercleri az olan masin lazimdi`

Sistem bu cümləni başa düşüb strukturlu filter-lərə çevirir və uyğun elanları qaytarır.

### Bu niyə güclüdür

- istifadəçi filter-lərlə çox oynamaq istəmir
- danışıq dilində axtarış daha təbii gəlir
- sayt “elan bazası” yox, “ağıllı seçim platforması” kimi görünür
- mobil istifadədə çox rahatdır

### Necə işləyər

1. istifadəçi mətn yazır
2. AI bunu parse edir
3. parse nəticəsi backend filter obyektinə çevrilir
4. elanlar həmin filter-lərlə qaytarılır
5. istəsək hər elanın yanında “niyə uyğun gəldi” izahı da göstərilir

### Misal

İstifadəçi yazır:

`mene 18-22 min arasi, 2018+, sedan, ekonom, aile ucun masin goster`

Sistem bunu belə çıxara bilər:

```json
{
  "priceMin": 18000,
  "priceMax": 22000,
  "yearMin": 2018,
  "bodyType": "sedan",
  "fuelEconomyPriority": true,
  "familyFriendly": true
}
```

### Edilmə asanlığı

`Orta`

Səbəb:

- ilk versiya üçün tam “chatbot” lazım deyil
- natural language -> filter parser kifayətdir
- bəzi anlayışlar mapping istəyir:
  - `ekonom`
  - `aile ucun`
  - `premium`
  - `birinci masin`
  - `rahat`

### MVP versiya

İlk versiyada bunları dəstəkləmək kifayətdir:

- qiymət aralığı
- il
- marka
- model
- kuzov növü
- yanacaq növü
- transmissiya
- yürüş

Sonra ikinci mərhələdə bu semantik anlayışları əlavə etmək olar:

- ailə üçün uyğundur
- tələbə üçün yaxşıdır
- ekonomdur
- şəhər üçün rahatdır
- premium hiss verir

### Texniki yanaşma

- frontend-də bir input
- backend-də `AiSearchRequest`
- AI parser servisi
- nəticədə standard `CarQueryFilter`
- sonra mövcud listing query sistemi ilə inteqrasiya

### Gələcək genişlənmə

Sonra bunu bunlara böyütmək olar:

- səsli axtarış
- follow-up suallar
- “bunu bir az daha ucuzlaşdır”
- “bunun əvəzinə daha ekonom variant göstər”

## Səni fərqləndirəcək əsas feature-lər

### 1. AI Search Assistant

Nə edir:

- danışıq dilində axtarışı başa düşür
- istifadəçiyə uyğun elanları tapır

İstifadəçi dəyəri:

- axtarış daha rahat olur
- filter-lərlə boğulmur

Turbo tipli saytdan fərqi:

- orada əsasən klassik filter təcrübəsi var
- burada intent-driven axtarış olur

Edilmə asanlığı:

`Orta`

MVP:

- text to filters
- uyğun elanların listi

### 2. AI Match Score

Nə edir:

Hər elanın yanında istifadəçiyə uyğunluq balı verir:

- `Sana uyğunluq: 91%`

və səbəbini yazır:

- büdcənə uyğundur
- ili sənin istəyinə uyğundur
- ailə üçün rahatdır

İstifadəçi dəyəri:

- seçim etmək sürətlənir
- çox elan arasında qərar vermək rahatlaşır

Edilmə asanlığı:

`Orta`

MVP:

- qayda əsaslı score
- qısa 2-3 səbəb



### 3. Trust Score / Verified Listing

Nə edir:

Elan üçün etibar balı hesablayır:

- satıcı verifikasiya olunub?
- şəkillər kifayət qədər keyfiyyətlidirmi?
- məlumatlar tamdırmı?
- VIN / servis / yürüş məntiqi görünürmü?

İstifadəçi dəyəri:

- daha etibarlı elanlar önə çıxır

Edilmə asanlığı:

`Orta`

MVP:

- profile verification
- complete listing score
- image count / quality score

###  4. Compare Cars

Nə edir:

2-4 maşını yan-yana müqayisə edir:

- qiymət
- il
- yürüş
- mühərrik
- yanacaq
- təchizat

İstifadəçi dəyəri:

- qərar vermək rahatlaşır

Edilmə asanlığı:

`Asan`

MVP:

- shortlist
- compare table

### 5. Ownership Cost Calculator

Nə edir:

Maşının tək alış qiymətini yox, ümumi xərcini göstərir:

- aylıq yanacaq
- servis xərci
- sığorta
- vergi

İstifadəçi dəyəri:

- “alım ya yox” qərarı daha real olur

Edilmə asanlığı:

`Orta`

MVP:

- təxmini yanacaq hesabı
- illik servis aralığı
- vergi/sığorta estimator







### 1. AI Search Assistant

Çünki:

- fərqli görünür
- real istifadəçi problemi həll edir
- mövcud backend-ə uyğunlaşdırmaq olur

### 2. Compare Cars

Çünki:

- tez hazırlanır
- UI-də dərhal dəyər verir

### 3. Trust Score

Çünki:

- elan keyfiyyətini artırır
- alıcı etibarını yüksəldir

## AI Search Assistant üçün konkret MVP

### İstifadəçi axını

1. istifadəçi yazır
2. sistem onu filter-lərə çevirir
3. uyğun elanları qaytarır
4. “niyə bunlar seçildi” göstərir

### Backend-də lazım olanlar

- `AiSearchRequest`
- `AiSearchResult`
- `AiSearchParserService`
- `CarSearchFilter`
- parser -> filter mapping

### Frontend-də lazım olanlar

- search input
- suggestion chips
- parsed filters preview
- nəticə listi

### Sonradan əlavə edilənlər

- follow-up questions
- search history
- saved prompts
- voice input

## AI Buying Assistant

Bu artıq daha böyük feature-dir.

İstifadəçi soruşa bilər:

- `bu masin aile ucun yaxsidir?`
- `bu qiymete deyer?`
- `bu modelin menfi cehetleri nelerdi?`

Bu ideya çox güclüdür, amma ikinci mərhələyə saxlamaq daha ağıllıdır. Çünki bunun üçün:

- model knowledge
- market context
- listing context
- prompt engineering
- cavab təhlükəsizliyi

lazımdır.

Ona görə doğru ardıcıllıq belədir:

1. AI Search Assistant
2. Match Score
3. AI Buying Assistant

## Nəticə

CarListing-in fərqi “daha çox filter” yox, “daha ağıllı seçim təcrübəsi” olmalıdır.

Ən güclü istiqamət:

- AI ilə istifadəçi niyyətini başa düşmək
- etibarlı elanları önə çıxarmaq
- alıcıya qərar vermək üçün əlavə layer vermək

Qısa desək, ən düzgün məhsul istiqaməti budur:

```text
Find faster
Trust more
Decide better
```
