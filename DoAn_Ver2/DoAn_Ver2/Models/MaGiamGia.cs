namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("MaGiamGia")]
    public partial class MaGiamGia
    {
        public int ID { get; set; }

        [StringLength(50)]
        public string MaCode { get; set; }

        [StringLength(155)]
        public string LoaiGiam { get; set; }

        public decimal? GiaTri { get; set; }

        public decimal? DonToiThieu { get; set; }

        public int? SoLuong { get; set; }

        public DateTime? NgayHetHan { get; set; }
    }
}
