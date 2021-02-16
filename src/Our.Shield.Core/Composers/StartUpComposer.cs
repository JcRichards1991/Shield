﻿using AutoMapper;
using LightInject;
using Our.Shield.Core.CacheRefreshers;
using Our.Shield.Core.Components;
using Our.Shield.Core.Data.Accessors;
using Our.Shield.Core.Services;
using System;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Our.Shield.Core.Composers
{
    /// <summary>
    /// Initializes Shield's Start Up requirements
    /// </summary>
    [RuntimeLevel(MinLevel = RuntimeLevel.Run)]
    public class StartUpComposer : IUserComposer
    {
        /// <inheritdoc/>
        public void Compose(Composition composition)
        {
            RegisterComponents(composition);
            RegisterDataAccessors(composition);
            RegisterServices(composition);
            RegisterCacheRefreshers(composition);
            RegisterAutoMapper(composition);
        }

        private void RegisterComponents(Composition composition)
        {
            composition.Components().Append<ClearCacheComponent>();
        }

        private void RegisterDataAccessors(Composition composition)
        {
            composition.Register<IEnvironmentAccessor, EnvironmentAccessor>();
            composition.Register<IAppAccessor, AppAccessor>();
            composition.Register<IJournalAccessor, JournalAccessor>();
        }

        private void RegisterServices(Composition composition)
        {
            composition.Register<IJobService, JobService>(Lifetime.Singleton);
            composition.Register<IEnvironmentService, EnvironmentService>();
        }

        private void RegisterCacheRefreshers(Composition composition)
        {
            composition.CacheRefreshers().Add<ConfigurationCacheRefresher>();
            composition.CacheRefreshers().Add<EnvironmentCacheRefresher>();
        }

        private void RegisterAutoMapper(Composition composition)
        {
            if (!(composition.Concrete is ServiceContainer container))
            {
                throw new ArgumentException(nameof(container));
            }

            container.Register(factory =>
               new MapperConfiguration(cfg =>
               {
                   cfg.AddProfile(new Mappers.GlobalProfile());
               })
               .CreateMapper(),
               nameof(Shield) + "Mapper",
               new PerContainerLifetime());
        }
    }
}