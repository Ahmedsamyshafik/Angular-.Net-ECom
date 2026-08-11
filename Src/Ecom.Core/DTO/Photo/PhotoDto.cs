namespace Ecom.Core.DTO.Photo
{
    public record PhotoDto
    {
        public int PhotoId { get; set; }
        public string PhotoPath { get; set; }
        public int ProductId { get; set; }
    }
}
