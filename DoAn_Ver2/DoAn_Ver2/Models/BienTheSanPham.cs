namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("BienTheSanPham")]
    public partial class BienTheSanPham
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public BienTheSanPham()
        {
            ChiTietDonHangs = new HashSet<ChiTietDonHang>();
            GioHangChiTiets = new HashSet<GioHangChiTiet>();
            LichSuKhoes = new HashSet<LichSuKho>();
        }

        public int ID { get; set; }

        public int SanPhamID { get; set; }

        public int MauSacID { get; set; }

        public int KichThuocID { get; set; }

        public int? SoLuong { get; set; }

        public int? SoLuongTamGiu { get; set; }

        [StringLength(100)]
        public string SKU { get; set; }

        public virtual KichThuoc KichThuoc { get; set; }

        public virtual MauSac MauSac { get; set; }

        public virtual SanPham SanPham { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ChiTietDonHang> ChiTietDonHangs { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<GioHangChiTiet> GioHangChiTiets { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<LichSuKho> LichSuKhoes { get; set; }
    }
}
