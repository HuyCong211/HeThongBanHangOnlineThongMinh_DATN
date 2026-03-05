namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DiaChi")]
    public partial class DiaChi
    {
        public int ID { get; set; }

        public int NguoiDungID { get; set; }

        [StringLength(200)]
        public string TenNguoiNhan { get; set; }

        [StringLength(10)]
        public string SDT_NguoiNhan { get; set; }

        [StringLength(255)]
        public string DiaChiChiTiet { get; set; }

        [StringLength(100)]
        public string PhuongXa { get; set; }

        [StringLength(100)]
        public string Tinh_ThanhPho { get; set; }

        public bool? MacDinh { get; set; }

        public virtual NguoiDung NguoiDung { get; set; }
    }
}
