namespace CallCadence.API.ApiCall
{
    public interface ISentryService
    {
        void CaptureExceptionDetailsForSentry(Exception exception, string errorMessage, string sentryTag);
        Task LogErrorMessage(string errorMessage, string sentryTag);
        void SetBreadcrumb(string breadcrumb);
        void SetExtra(string key, object? value);
        void SetApiTags(HttpRequestMessage request);
    }
}
