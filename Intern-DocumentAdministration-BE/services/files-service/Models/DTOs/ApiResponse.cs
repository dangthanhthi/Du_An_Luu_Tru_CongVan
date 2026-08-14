using System.Collections.Generic;

namespace FilesService.Models.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; } //
        public T Data { get; set; } //
        public string Message { get; set; } //[cite: 2]
        public List<string> Errors { get; set; } = new List<string>(); //[cite: 2]
    }
}