using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Linq.Expressions;

namespace DoAn_Ver2.Infrastructure
{
    public interface IRepository<T> where T : class
    {
        // Lấy tất cả
        IQueryable<T> GetAll();

        // Lấy theo ID
        T GetById(object id);

        // Tìm kiếm có điều kiện (Expression lambda)
        IQueryable<T> GetMany(Expression<Func<T, bool>> where);

        // Thêm
        void Add(T entity);

        // Sửa
        void Update(T entity);

        // Xóa
        void Delete(T entity);
        void Delete(object id);
    }
}