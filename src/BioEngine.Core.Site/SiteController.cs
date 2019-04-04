using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BioEngine.Core.DB;
using BioEngine.Core.Entities;
using BioEngine.Core.Properties;
using BioEngine.Core.Repository;
using BioEngine.Core.Site.Filters;
using BioEngine.Core.Site.Model;
using BioEngine.Core.Storage;
using BioEngine.Core.Web;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BioEngine.Core.Site
{
    public abstract class SiteController<TEntity> : BaseController where TEntity : class, IEntity
    {
        protected SiteController(SiteControllerContext<TEntity> context) : base(context)
        {
            Repository = context.Repository;
            PageFilters = context.PageFilters;
            FeaturesCollection = context.FeaturesCollection;
        }

        protected PageFeaturesCollection FeaturesCollection { get; set; }
        protected int Page { get; private set; } = 1;
        protected const int ItemsPerPage = 1;

        [PublicAPI] protected IBioRepository<TEntity> Repository;
        [PublicAPI] protected IEnumerable<IPageFilter> PageFilters;

        private Entities.Site Site
        {
            get
            {
                var siteFeature = HttpContext.Features.Get<CurrentSiteFeature>();
                if (siteFeature == null)
                {
                    throw new ArgumentException("CurrentSiteFeature is empty");
                }

                return siteFeature.Site;
            }
        }

        [HttpGet]
        public virtual async Task<IActionResult> ListAsync()
        {
            var (items, itemsCount) = await Repository.GetAllAsync(GetQueryContext());
            return View("List",
                new ListViewModel<TEntity>(await GetPageContextAsync(items), items,
                    itemsCount, Page, ItemsPerPage));
        }

        protected virtual async Task<PageViewModelContext> GetPageContextAsync(TEntity[] entities)
        {
            var context = new PageViewModelContext(PropertiesProvider, FeaturesCollection, Site);
            if (PageFilters != null && PageFilters.Any())
            {
                foreach (var pageFilter in PageFilters)
                {
                    await pageFilter.ProcessPageAsync(context);
                    if (pageFilter.CanProcess(typeof(TEntity)))
                    {
                        await pageFilter.ProcessEntitiesAsync(context, entities);
                    }
                }
            }

            return context;
        }

        [HttpGet("{id}-{url}.html")]
        public virtual async Task<IActionResult> ShowAsync(Guid id, string url)
        {
            var entity = await Repository.GetByIdAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            return View("Show",
                new EntityViewModel<TEntity>(await GetPageContextAsync(new[] {entity}), entity));
        }

        [PublicAPI]
        protected QueryContext<TEntity> GetQueryContext()
        {
            var context = new QueryContext<TEntity> {Limit = ItemsPerPage};
            if (ControllerContext.HttpContext.Request.Query.ContainsKey("page"))
            {
                Page = int.Parse(ControllerContext.HttpContext.Request.Query["page"]);
                if (Page < 1) Page = 1;
                context.Offset = (Page - 1) * ItemsPerPage;
            }

            if (ControllerContext.HttpContext.Request.Query.ContainsKey("order"))
            {
                context.SetOrderByString(ControllerContext.HttpContext.Request.Query["order"]);
            }
            else
            {
                context.SetOrderByDescending(e => e.DatePublished);
            }

            return context;
        }
    }

    public class SiteControllerContext<TEntity> : BaseControllerContext<TEntity>
        where TEntity : class, IEntity
    {
        public IEnumerable<IPageFilter> PageFilters { get; }
        public PageFeaturesCollection FeaturesCollection { get; }

        public SiteControllerContext(ILoggerFactory loggerFactory, IStorage storage,
            PropertiesProvider propertiesProvider,
            IBioRepository<TEntity> repository, IEnumerable<IPageFilter> pageFilters,
            PageFeaturesCollection featuresCollection) : base(loggerFactory,
            storage,
            propertiesProvider, repository)
        {
            PageFilters = pageFilters;
            FeaturesCollection = featuresCollection;
        }
    }
}
