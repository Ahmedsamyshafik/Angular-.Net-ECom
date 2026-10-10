using System;
using System.Collections.Generic;

namespace Ecom.Core.Pagination
{
    /// <summary>
    /// A reusable paged result wrapper. TotalPages is derived from the
    /// total record count and the page size; it is 0 when there are no records.
    /// </summary>
    public sealed class Pagination<T>
    {
        public Pagination(int pageNumber, int pageSize, int totalCount, IReadOnlyList<T> data)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");
            }

            if (pageSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
            }

            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalCount = totalCount;
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int PageNumber { get; }
        public int PageSize { get; }
        public int TotalCount { get; }
        public int TotalPages { get; }
        public IReadOnlyList<T> Data { get; }
    }
}