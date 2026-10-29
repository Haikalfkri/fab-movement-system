using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ok1.Models.ApiResponse
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
        public PaginationMeta Pagination { get; set; }
        public string Error { get; set; }
    }

    public class PaginationMeta
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
    }
}