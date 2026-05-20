# Frontend API Contract

Tarix: 2026-05-17

Bu sənəd frontend üçün lazım olan backend endpoint-ləri, DTO-ları və istifadə axınlarını toplayır.

## Base

Development API URL layihənin launch profile/port ayarından asılıdır.

Swagger:

```text
/swagger
```

Protected endpoint-lərdə header:

```http
Authorization: Bearer {token}
```

## Car Fields

Frontend car form və UI bu field-lərlə işləməlidir:

```ts
type Car = {
  id: number;
  createdAt: string;
  title: string;
  brand: string;
  model: string;
  year: number;
  price: number;
  mileage: number;
  fuelType: string;
  bodyType: string;
  transmission: string;
  engine: string;
  city: string;
  color?: string | null;
  description?: string | null;
  userId: number;
  images: CarImageDto[];
  features: CarFeatureDto[];
};
```

Create/update body:

```ts
type CreateCarDto = {
  title: string;
  brand: string;
  model: string;
  year: number;
  price: number;
  mileage: number;
  fuelType: string;
  bodyType: string;
  transmission: string;
  engine: string;
  city: string;
  color?: string | null;
  description?: string | null;
  features: CreateCarFeatureDto[];
};
```

Validation:

- `title`: required, max 200
- `brand`: required, max 50
- `model`: required, max 50
- `year`: 1950-2100
- `price`: min 0.01
- `mileage`: 0-2,000,000
- `fuelType`: required, max 30
- `bodyType`: required, max 30
- `transmission`: required, max 30
- `engine`: required, max 50
- `city`: required, max 50
- `color`: max 50
- `description`: max 2000

## Shared Types

```ts
type PagedResultDto<T> = {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
};

type CarFeatureDto = {
  id: number;
  key: string;
  value: string;
};

type CreateCarFeatureDto = {
  key: string;
  value: string;
};

type CarImageDto = {
  id: number;
  imageUrl: string;
  isMain: boolean;
  order: number;
};
```

## Metadata

Dropdown/select list-lər üçün:

```http
GET /api/cars/metadata
```

Response:

```ts
type CarMetadataDto = {
  fuelTypes: string[];
  bodyTypes: string[];
  transmissions: string[];
  sortOptions: string[];
};
```

Hazır dəyərlər:

- fuelTypes: `Petrol`, `Diesel`, `Hybrid`, `Electric`
- bodyTypes: `Sedan`, `SUV`, `Hatchback`, `Coupe`, `Wagon`, `Minivan`, `Pickup`
- transmissions: `Automatic`, `Manual`
- sortOptions: `newest`, `price`, `year`, `mileage`

## Listing/Search

```http
GET /api/cars
```

Query params:

```ts
type CarSearchFilterDto = {
  page?: number;
  pageSize?: number;
  search?: string;
  brand?: string;
  model?: string;
  city?: string;
  fuelType?: string;
  bodyType?: string;
  transmission?: string;
  priceMin?: number;
  priceMax?: number;
  yearMin?: number;
  yearMax?: number;
  mileageMax?: number;
  sortBy?: 'newest' | 'price' | 'year' | 'mileage';
  sortDirection?: 'asc' | 'desc';
};
```

Example:

```http
GET /api/cars?page=1&pageSize=20&brand=Toyota&priceMax=20000&yearMin=2019&mileageMax=90000&sortBy=price&sortDirection=asc
```

Response:

```ts
type CarListItemDto = {
  id: number;
  title: string;
  brand: string;
  model: string;
  year: number;
  price: number;
  mileage: number;
  fuelType: string;
  bodyType: string;
  transmission: string;
  engine: string;
  city: string;
  color?: string | null;
  createdAt: string;
  features: CarFeatureDto[];
};
```

Response wrapper:

```ts
PagedResultDto<CarListItemDto>
```

## Car Detail

```http
GET /api/cars/{id}
```

Response:

```ts
type CarDetailsDto = {
  id: number;
  createdAt: string;
  title: string;
  brand: string;
  model: string;
  year: number;
  price: number;
  mileage: number;
  fuelType: string;
  bodyType: string;
  transmission: string;
  engine: string;
  city: string;
  color?: string | null;
  description?: string | null;
  userId: number;
  images: CarImageDto[];
  features: CarFeatureDto[];
};
```

## Create Car

Protected.

```http
POST /api/cars
Authorization: Bearer {token}
Content-Type: application/json
```

Body:

```ts
CreateCarDto
```

Response:

```json
{ "id": 123 }
```

## Update Car

Protected. Owner or admin.

```http
PUT /api/cars/{id}
Authorization: Bearer {token}
Content-Type: application/json
```

Body:

```ts
CreateCarDto
```

Success:

```http
204 No Content
```

## Delete Car

Protected. Owner or admin.

```http
DELETE /api/cars/{id}
Authorization: Bearer {token}
```

Success:

```http
204 No Content
```

Qeyd: soft delete edir.

## Images

Upload:

```http
POST /api/cars/{id}/images
Authorization: Bearer {token}
Content-Type: multipart/form-data
```

Form field:

```text
files
```

Multiple files supported.

Allowed:

- jpg/jpeg
- png
- webp
- avif
- max 5MB each

Response:

```ts
type UploadImagesResponse = {
  carId: number;
  images: CarImageDto[];
};
```

Set main image:

```http
PUT /api/cars/{id}/images/{imageId}/main
Authorization: Bearer {token}
```

Delete image:

```http
DELETE /api/cars/{id}/images/{imageId}
Authorization: Bearer {token}
```

Reorder:

```http
PUT /api/cars/{id}/images/reorder
Authorization: Bearer {token}
Content-Type: application/json
```

Body:

```ts
type ReorderImagesDto = {
  imageIds: number[];
};
```

Important: `imageIds` car-a aid bütün image id-ləri exact bir dəfə göndərilməlidir.

## Compare Cars

```http
GET /api/cars/compare?ids=1&ids=2
```

Rules:

- minimum 2 ids
- max 4 ids used
- only active cars
- selected car missing/inactive olsa `400 BadRequest`

Response:

```ts
type CarCompareItemDto = {
  id: number;
  title: string;
  brand: string;
  model: string;
  year: number;
  price: number;
  mileage: number;
  fuelType: string;
  bodyType: string;
  transmission: string;
  engine: string;
  city: string;
  color?: string | null;
  features: CarFeatureDto[];
};
```

Example:

```http
GET /api/cars/compare?ids=3&ids=8&ids=12
```

## AI Search Parse

```http
POST /api/cars/ai-search/parse
Content-Type: application/json
```

Body:

```ts
type AiSearchRequestDto = {
  query: string;
};
```

Example:

```json
{
  "query": "Toyota 18-22 min 2019+ sedan avtomat"
}
```

Response:

```ts
type AiSearchResultDto = {
  query: string;
  filter: CarSearchFilterDto;
  notes: string[];
};
```

Frontend flow:

1. User text yazır.
2. `/api/cars/ai-search/parse` çağırılır.
3. `response.filter` götürülür.
4. Həmin filter ilə `/api/cars` çağırılır.

## Auth

Register:

```http
POST /api/auth/register
Content-Type: application/json
```

Body:

```ts
type RegisterDto = {
  username: string;
  email: string;
  password: string;
};
```

Response:

```ts
type AuthResponseDto = {
  token: string;
};
```

Login:

```http
POST /api/auth/login
Content-Type: application/json
```

Body:

```ts
type LoginDto = {
  email: string;
  password: string;
};
```

Response:

```ts
AuthResponseDto
```

## Admin

Admin token required.

Block/unblock user:

```http
PUT /api/admin/users/{id}/block?block=true
Authorization: Bearer {token}
```

Activate/deactivate car:

```http
PUT /api/admin/cars/{id}/active?active=true
Authorization: Bearer {token}
```

Admin cars list:

```http
GET /api/admin/cars?active=true
Authorization: Bearer {token}
```

## Frontend Build Order

1. Auth pages: register/login/token storage.
2. Listing page with `/api/cars`.
3. Filters using `/api/cars/metadata`.
4. Car detail page.
5. Create/update form.
6. Image upload/manage.
7. Compare selected cars.
8. AI search input that maps parse result into listing filters.
