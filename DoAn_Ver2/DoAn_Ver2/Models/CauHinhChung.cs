namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("CauHinhChung")]
    public partial class CauHinhChung
    {
        [Key]
        [StringLength(255)]
        public string KeyName { get; set; }

        public string Value { get; set; }

        [StringLength(255)]
        public string MoTa { get; set; }
    }
}
