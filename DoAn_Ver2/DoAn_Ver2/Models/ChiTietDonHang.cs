namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("ChiTietDonHang")]
    public partial class ChiTietDonHang
    {
        public int ID { get; set; }

        public int DonHangID { get; set; }

        public int BienTheSanPhamID { get; set; }

        public int? SoLuong { get; set; }

        public decimal? DonGia { get; set; }

        public decimal? ThanhTien { get; set; }

        public virtual BienTheSanPham BienTheSanPham { get; set; }

        public virtual DonHang DonHang { get; set; }
    }
}
