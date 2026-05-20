using System.ComponentModel.DataAnnotations;
using Entry.Dto;

namespace CarListing.Tests;

public class DtoValidationTests
{
    [Fact]
    public void RegisterDto_IsInvalid_WhenEmailIsMissing()
    {
        var dto = new RegisterDto
        {
            Username = "kamal",
            Email = "",
            Password = "secret123"
        };

        Assert.False(IsValid(dto));
    }

    [Fact]
    public void CreateCarDto_IsInvalid_WhenYearIsOutOfRange()
    {
        var dto = new CreateCarDto
        {
            Title = "Clean sedan",
            Brand = "Toyota",
            Model = "Camry",
            Year = 1900,
            Price = 12_000,
            Mileage = 80_000,
            FuelType = "Petrol",
            BodyType = "Sedan",
            Transmission = "Automatic",
            Engine = "2.0",
            City = "Baku"
        };

        Assert.False(IsValid(dto));
    }

    [Fact]
    public void CreateCarDto_IsValid_WithRequiredFields()
    {
        var dto = new CreateCarDto
        {
            Title = "Clean sedan",
            Brand = "Toyota",
            Model = "Camry",
            Year = 2020,
            Price = 12_000,
            Mileage = 80_000,
            FuelType = "Petrol",
            BodyType = "Sedan",
            Transmission = "Automatic",
            Engine = "2.0",
            City = "Baku"
        };

        Assert.True(IsValid(dto));
    }

    private static bool IsValid(object value)
    {
        var context = new ValidationContext(value);
        return Validator.TryValidateObject(value, context, validationResults: null, validateAllProperties: true);
    }
}
