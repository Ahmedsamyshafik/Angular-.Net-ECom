namespace ECom.Api.Helper
{
    public class responseApi
    {
        public responseApi(int statusCode, string? statusMessage = null, object? data = null)
        {
            StatusCode = statusCode;
            StatusMessage = statusMessage ?? GetMSGFromStatusCode(statusCode);
            Data = data;
        }

        public int StatusCode { get; set; }
        public string? StatusMessage { get; set; }
        public object? Data { get; set; }

        private string? GetMSGFromStatusCode(int statusCode)
        {
            return statusCode switch
            {
                200 => "Success",
                400 => "Bad Request",
                401 => "Unauthorized",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => "Unknown Error"
            };
        }
    }
}
