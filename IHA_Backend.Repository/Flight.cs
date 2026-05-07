using System;
using System.Collections.Generic;
using System.Text;

namespace IHA_Backend.Repository
{
    public class Flight : ICrudInterface
    {
        public Task<bool> Add<T>(T t) where T : class
        {
            throw new NotImplementedException();
        }
    }
}
