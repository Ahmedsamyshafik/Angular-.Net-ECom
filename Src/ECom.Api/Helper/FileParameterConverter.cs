using Ecom.Core.DTO.Common;

namespace ECom.Api.Helper
{
    public static class FileParameterConverter
    {
        public static FileParameterDto ToFileParameter(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            file.CopyTo(memoryStream);

            return new FileParameterDto
            {
                Data = memoryStream.ToArray(),
                FileName = file.FileName,
                ContentType = file.ContentType
            };
        }
    }
}
