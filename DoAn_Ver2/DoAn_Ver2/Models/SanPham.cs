namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("SanPham")]
    public partial class SanPham
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public SanPham()
        {
            AnhSanPhams = new HashSet<AnhSanPham>();
            BienTheSanPhams = new HashSet<BienTheSanPham>();
            DanhGias = new HashSet<DanhGia>();
            GoiYSanPhams = new HashSet<GoiYSanPham>();
            GoiYSanPhams1 = new HashSet<GoiYSanPham>();
            LichSuXems = new HashSet<LichSuXem>();
            SanPhamYeuThiches = new HashSet<SanPhamYeuThich>();
        }

        public int ID { get; set; }

        [StringLength(50, ErrorMessage = "Mã sản phẩm không được vượt quá 50 ký tự")]
        [Required(ErrorMessage = "Mã sản phẩm không được để trống")]
        public string MaSanPham { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        [StringLength(200, ErrorMessage = "Tên sản phẩm không được vượt quá 200 ký tự")]
        public string TenSanPham { get; set; }

        [StringLength(200)]
        public string Slug { get; set; }

        public string MoTa { get; set; }

        public int? DanhMucID { get; set; }

        public decimal? GiaGoc { get; set; }
        [Required(ErrorMessage = "Giá bán không được để trống")] 
        [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn 0")]
        public decimal? GiaBan { get; set; }

        [StringLength(255, ErrorMessage = "Chất liệu sản phẩm không được vượt quá 255 ký tự")]
        public string ChatLieu { get; set; }

        [StringLength(255, ErrorMessage = "Kiểu dáng sản phẩm không được vượt quá 255 ký tự")]
        public string KieuDang { get; set; }

        public int? LuotXem { get; set; }

        public int? TrangThai { get; set; }

        public DateTime? NgayTao { get; set; }

        public DateTime? NgayCapNhat { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<AnhSanPham> AnhSanPhams { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<BienTheSanPham> BienTheSanPhams { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<DanhGia> DanhGias { get; set; }

        public virtual DanhMuc DanhMuc { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<GoiYSanPham> GoiYSanPhams { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<GoiYSanPham> GoiYSanPhams1 { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<LichSuXem> LichSuXems { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SanPhamYeuThich> SanPhamYeuThiches { get; set; }
    }
}
