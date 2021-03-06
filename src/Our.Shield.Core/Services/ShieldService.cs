﻿using Our.Shield.Core.Models;
using System;
using System.Linq;

namespace Our.Shield.Core.Services
{
    public class ShieldService
    {
        public IEnvironment GetEnvironmentByKey(Guid key)
        {
            return JobService
                .Instance
                .Environments
                .FirstOrDefault(x => x.Key.Key == key)
                .Key;
        }

        public IEnvironment GetEnvironmentByAppKey(Guid appKey)
        {
            return JobService
                .Instance
                .Environments
                .FirstOrDefault(x => x.Value.Any(y => y.Key == appKey))
                .Key;
        }
    }
}
