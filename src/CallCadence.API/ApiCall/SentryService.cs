namespace CallCadence.API.ApiCall
{
    public class SentryService : ISentryService
    {
        /// <summary>
        /// Captures exception details and sends them to Sentry with additional context information.
        /// </summary>
        /// <param name="exception">Exception error</param>
        /// <param name="errorMessage">Additional details for the Sentry Log</param>
        /// <param name="sentryTag">Tag to identify the Sentry log for One Notifications</param>
        /// <returns></returns>
        public void CaptureExceptionDetailsForSentry(Exception exception, string errorMessage, string sentryTag)
        {
            SentrySdk.CaptureException(exception, scope =>
            {
                scope.SetExtra("ErrorMessage", errorMessage);
                scope.SetTag("OneLookup", sentryTag);
            });

        }

        public async Task LogErrorMessage(string errorMessage, string sentryTag)
        {
            Exception exception = new Exception(errorMessage);
            CaptureExceptionDetailsForSentry(exception, errorMessage, sentryTag);
        }
        public void SetBreadcrumb(string breadcrumb)
        {
            SentrySdk.ConfigureScope(scope =>
            {
                scope.AddBreadcrumb(breadcrumb);
            });
        }
        public void SetExtra(string key, object value)
        {
            SentrySdk.ConfigureScope(scope =>
            {
                scope.SetExtra(key, value);
            });
        }

        public void SetApiTags(HttpRequestMessage request)
        {
            SentrySdk.ConfigureScope(scope =>
            {
                Dictionary<string, string> requestSentryTags = BuildHttpRequestSentryTags(request);
                foreach (var tag in requestSentryTags)
                {
                    scope.SetTag(tag.Key, tag.Value);
                }
            });
        }

        private Dictionary<string, string> BuildHttpRequestSentryTags(HttpRequestMessage request)
        {
            Dictionary<string, string> requestSentryTags = new Dictionary<string, string>();
            requestSentryTags.Add("HttpRequestMethod", request.Method.ToString());
            requestSentryTags.Add("HttpRequestUrl", request.RequestUri.ToString());
            requestSentryTags.Add("Headers", string.Join(", ", request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
            return requestSentryTags;
        }
    }
}