using EduSathi.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace EduSathi.ViewModels
{
    public class ExamDashboardViewModel
    {
        // For uploading a new PDF from the device
        public IFormFile? NewPdfFile { get; set; }

        // For selecting a previously uploaded PDF from storage
        public int? SelectedExistingDocumentId { get; set; }

        // History list for the dropdown selection
        public List<UploadedDocument> PreviousDocuments { get; set; } = new List<UploadedDocument>();
    }
}