using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Extensions
{
    // See: https://github.com/Azure-Samples/active-directory-dotnet-native-aspnetcore/blob/master/TodoListService/Extensions/AzureAdAuthenticationBuilderExtensions.cs
    public static class AzureAdServiceCollectionExtensions
    {
        public static AuthenticationBuilder AddAzureAdBearer(this AuthenticationBuilder builder)
            => builder.AddAzureAdBearer(_ => { });

        public static AuthenticationBuilder AddAzureAdBearer(this AuthenticationBuilder builder, Action<AzureAdOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);
            builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureAzureOptions>();
            builder.AddJwtBearer();
            return builder;
        }

        private class ConfigureAzureOptions : IConfigureNamedOptions<JwtBearerOptions>
        {
            private readonly AzureAdOptions _azureOptions;

            public ConfigureAzureOptions(IOptions<AzureAdOptions> azureOptions)
            {
                _azureOptions = azureOptions.Value;
            }

            public void Configure(string name, JwtBearerOptions options)
            {
                options.Audience = _azureOptions.ClientId;
                options.Authority = $"{_azureOptions.Instance}{_azureOptions.TenantId}";

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        OnMessageReceived(context);

                        return Task.CompletedTask;
                    }
                };
            }

            public void Configure(JwtBearerOptions options)
            {
                Configure(Options.DefaultName, options);
            }

            private void OnMessageReceived(MessageReceivedContext context)
            {
                // Try to parse the Basic Authorization crendentials.
                // See https://github.com/aspnet/Security/blob/beaa2b443d46ef8adaf5c2a89eb475e1893037c2/src/Microsoft.AspNetCore.Authentication.JwtBearer/JwtBearerHandler.cs#L61
                string authorization = context.HttpContext.Request.Headers["Authorization"];

                if (string.IsNullOrEmpty(authorization))
                {
                    return;
                }

                if (!authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var encodedCredentials = authorization.Substring("Basic ".Length).Trim();
                var crendentials = Encoding.ASCII.GetString(Convert.FromBase64String(encodedCredentials));

                // The JSON Web Token is the Basic Auth password.
                if (!crendentials.StartsWith("BaGet:"))
                {
                    return;
                }

                context.Token = crendentials.Substring("BaGet:".Length);
            }
        }
    }
}
