using GeocoderSolution.DTOs;
using GeocoderSolution.Services;
using Microsoft.AspNetCore.Mvc;

namespace GeocoderSolution.Controllers;

[ApiController]
[Route("geocode")]
public sealed class GeocodingController(GeocodingService geocodingService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<GeocodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeocodeResponse>> Geocode(GeocodeRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = Validate(request);

        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        var response = await geocodingService.GeocodeAsync(request, cancellationToken);

        return Ok(response);
    }

    private static Dictionary<string, string[]> Validate(GeocodeRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Addresses is null)
        {
            errors["addresses"] = ["The addresses field is required."];
            return errors;
        }

        if (request.Addresses.Count == 0)
        {
            errors["addresses"] = ["At least one address is required."];
        }

        for (var index = 0; index < request.Addresses.Count; index++)
        {
            var input = request.Addresses[index];

            if (input is null)
            {
                errors[$"addresses[{index}]"] = ["An address item is required."];
                continue;
            }

            if (string.IsNullOrWhiteSpace(input.Id))
            {
                errors[$"addresses[{index}].id"] = ["The id field is required."];
            }

            if (string.IsNullOrWhiteSpace(input.Address))
            {
                errors[$"addresses[{index}].address"] = ["The address field is required."];
            }
        }

        return errors;
    }
}
