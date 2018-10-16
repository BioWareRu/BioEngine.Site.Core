using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BioEngine.Core.DB;
using BioEngine.Core.Interfaces;
using BioEngine.Core.Providers;
using BioEngine.Core.Site.Filters;
using BioEngine.Core.Site.Model;
using BioEngine.Core.Web;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BioEngine.Core.Site
{
    public abstract class SiteController<TEntity, TEntityPk> : BaseController where TEntity : class, IEntity<TEntityPk>
    {
        protected SiteController(SiteControllerContext<TEntity, TEntityPk> context) : base(context)
        {
            Repository = context.Repository;
            PageFilters = context.PageFilters;
        }

        [PublicAPI] protected IBioRepository<TEntity, TEntityPk> Repository;
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
        public virtual async Task<IActionResult> List()
        {
            var result = await Repository.GetAll(GetQueryContext());
            return View("List",
                new ListViewModel<TEntity, TEntityPk>(await GetPageContext(result.items.ToArray()), result.items,
                    result.itemsCount));
        }

        protected virtual async Task<PageViewModelContext> GetPageContext(TEntity[] entities)
        {
            var context = new PageViewModelContext(SettingsProvider, Site);
            if (PageFilters != null && PageFilters.Any())
            {
                foreach (var pageFilter in PageFilters)
                {
                    await pageFilter.ProcessPage(context);
                    if (pageFilter.CanProcess(typeof(TEntity)))
                    {
                        await pageFilter.ProcessEntities<TEntity, TEntityPk>(context, entities);
                    }
                }
            }

            return context;
        }

        [HttpGet("{id}-{url}.html")]
        public virtual async Task<IActionResult> Show(TEntityPk id, string url)
        {
            var entity = await Repository.GetById(id);
            if (entity == null)
            {
                return NotFound();
            }

            return View("Show", new EntityViewModel<TEntity, TEntityPk>(await GetPageContext(new[] {entity}), entity));
        }

        [PublicAPI]
        protected QueryContext<TEntity, TEntityPk> GetQueryContext()
        {
            var context = new QueryContext<TEntity, TEntityPk>();
            if (ControllerContext.HttpContext.Request.Query.ContainsKey("limit"))
            {
                context.Limit = int.Parse(ControllerContext.HttpContext.Request.Query["limit"]);
            }

            if (ControllerContext.HttpContext.Request.Query.ContainsKey("offset"))
            {
                context.Offset = int.Parse(ControllerContext.HttpContext.Request.Query["offset"]);
            }

            if (ControllerContext.HttpContext.Request.Query.ContainsKey("order"))
            {
                context.SetOrderByString(ControllerContext.HttpContext.Request.Query["order"]);
            }

            return context;
        }
    }

    public class SiteControllerContext<TEntity, TEntityPk> : BaseControllerContext<TEntity, TEntityPk>
        where TEntity : class, IEntity<TEntityPk>
    {
        public IEnumerable<IPageFilter> PageFilters { get; }

        public SiteControllerContext(ILoggerFactory loggerFactory, IStorage storage, SettingsProvider settingsProvider,
            IBioRepository<TEntity, TEntityPk> repository, IEnumerable<IPageFilter> pageFilters) : base(loggerFactory, storage,
            settingsProvider, repository)
        {
            PageFilters = pageFilters;
        }
    }
}