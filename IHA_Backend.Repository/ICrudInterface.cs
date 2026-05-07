using System;
using System.Collections.Generic;
using System.Text;

namespace IHA_Backend.Repository
{
    public interface ICrudInterface
    {
        Task<bool> Add<T>(T t) where T : class;

    }
}
