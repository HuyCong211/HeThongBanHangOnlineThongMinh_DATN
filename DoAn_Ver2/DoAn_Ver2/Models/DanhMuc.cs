namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DanhMuc")]
    public partial class DanhMuc
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public DanhMuc()
        {
            DanhMuc1 = new HashSet<DanhMuc>();
            SanPhams = new HashSet<SanPham>();
        }

        public int ID { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(255)]
        public string TenDanhMuc { get; set; }

        [StringLength(255)]
        public string Slug { get; set; }

        public int? DanhMucChaID { get; set; }

        [StringLength(255)]
        public string MoTa { get; set; }

        [StringLength(255)]
        public string HinhAnh { get; set; }
        public int? TrangThai { get; set; }

        public DateTime? NgayTao { get; set; }

        public DateTime? NgayCapNhat { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<DanhMuc> DanhMuc1 { get; set; }

        public virtual DanhMuc DanhMuc2 { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SanPham> SanPhams { get; set; }
    }
}
