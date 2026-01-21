namespace SFARS.API.Extension
{
    public static class SwaggerFeatureExtensions
    {
        public static WebApplication WithSwagger(this WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json",
                    "SFARS API V1");
            });

            return app;
        }
    }
}
