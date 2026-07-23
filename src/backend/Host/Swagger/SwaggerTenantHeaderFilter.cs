using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERPSystem.Host.Swagger;

/// <summary>
/// يضيف X-Tenant-Id header إلى جميع الـ operations في Swagger UI
/// ليس إجبارياً — endpoints المصادقة تتجاهله، باقي الـ endpoints تستخدمه كمرجع.
/// </summary>
public sealed class SwaggerTenantHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-Id",
            In = ParameterLocation.Header,
            Required = false,
            Description = "معرّف المستأجر (اختياري — يُستخرج تلقائياً من JWT للأمان)",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
