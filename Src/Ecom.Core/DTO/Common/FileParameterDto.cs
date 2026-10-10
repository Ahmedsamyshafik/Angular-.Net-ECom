namespace Ecom.Core.DTO.Common
{
    public record FileParameterDto
    {
        public byte[] Data { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
    }
}
