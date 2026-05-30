using System;

namespace EnvContract.DTO.Entities
{
    public class EmailQueueDTO
    {
        public string EmailID { get; set; }
        public string Recipient { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string AttachmentPath { get; set; }
        public int? Status { get; set; }
        public DateTime? CreatedTime { get; set; }
        public string Type { get; set; }
    }
}
