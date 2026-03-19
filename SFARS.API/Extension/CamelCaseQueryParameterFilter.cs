using System.Text.Json;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SFARS.API.Extension;

public class CamelCaseQueryParameterFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters == null) return;

        foreach (var parameter in operation.Parameters)
        {
            // Only apply to query parameters with a name
            if (parameter.In == ParameterLocation.Query && !string.IsNullOrEmpty(parameter.Name))
            {
                //Use System.Text.Json's CamelCase naming policy to convert the parameter name to camelCase.
                // This ensures that the Swagger documentation reflects the actual query parameter names used in the API.
                parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name);
            }
        }
    }
}