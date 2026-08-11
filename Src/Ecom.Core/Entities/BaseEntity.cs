using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Ecom.Core.Entities
{
    public class BaseEntity<T>
    {
        public T Id { get; set; }
        public DateTime CreatedAt { get; set; }= DateTime.UtcNow;
        public T? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public T? UpdatedBy { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public T? DeletedBy { get; set; }



        [Timestamp]
        public byte[]? RowVersion { get; set; }
        


    }
}
