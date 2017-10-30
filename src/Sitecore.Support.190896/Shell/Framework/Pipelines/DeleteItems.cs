using Sitecore.Web;

namespace Sitecore.Support.Shell.Framework.Pipelines
{
  using System;
  using System.Linq;
  using System.Collections.Generic;
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Data.Managers;
  using Sitecore.Data.Templates;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.Links;
  using Sitecore.Text;
  using Sitecore.Web.UI.Sheer;

  public class DeleteItems : Sitecore.Shell.Framework.Pipelines.DeleteItems
  {
    public new void CheckLinks([NotNull] ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      switch (args.Result)
      {
        case "yes":
          args.Result = string.Empty;
          break;

        case "undefined":
        case "no":
          args.AbortPipeline();
          break;

        default:
          #region Changed code
          //if (DeleteItems.HasLinks(GetItems(args))) 
          if (Sitecore.Support.Shell.Framework.Pipelines.DeleteItems.HasLinks(GetItems(args))) 
          #endregion
          {
            var url = new UrlString(UIUtil.GetUri("control:BreakingLinks"));

            var list = new ListString(args.Parameters["items"], '|');

            url.Append("ignoreclones", "1");

            var urlHandle = new UrlHandle();
            urlHandle["list"] = list.ToString();
            urlHandle.Add(url);

            SheerResponse.ShowModalDialog(url.ToString(), true);

            args.WaitForPostBack();
          }

          break;
      }
    }

    public new void CheckTemplateLinks([NotNull] ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      List<Item> items = GetItems(args);

      foreach (Item item in items)
      {
        if (!item.Paths.Path.StartsWith("/sitecore/templates", StringComparison.InvariantCulture))
        {
          continue;
        }

        #region Added code
        Template tmp = TemplateManager.GetTemplate(item.ID, item.Database);
        // if the template inherits /sitecore/templates/System/Layout/Rendering Parameters/Standard Rendering Parameters and has no usages
        if (tmp.InheritsFrom(new ID("{8CA06D6A-B353-44E8-BC31-B528C7306971}")) && !tmp.GetDescendants().Any())
        {
          continue;
        } 
        #endregion

        if (!VerifyNoTemplateLinks(args, item))
        {
          break;
        }
      }
    }

    private static bool HasLink([NotNull] LinkDatabase linkDatabase, [NotNull] Item item)
    {
      Assert.ArgumentNotNull(linkDatabase, "linkDatabase");
      Assert.ArgumentNotNull(item, "item");

      var links = linkDatabase.GetReferrers(item);
      if (links.Length > 0)
      {
        foreach (var link in links)
        {
          if (link.SourceFieldID != FieldIDs.Source && link.SourceFieldID != FieldIDs.SourceItem)
          {
            return true;
          }
        }
      }

      foreach (Item child in item.Children)
      {
        var result = HasLink(linkDatabase, child);
        if (result)
        {
          return true;
        }
      }

      return false;
    }

    public new static bool HasLinks(List<Item> items)
    {
      Assert.ArgumentNotNull(items, "items");
      var linkDatabase = Globals.LinkDatabase;
      foreach (var item in items)
      {
        #region Changed code
        // if (item == null || item.TemplateID == TemplateIDs.Template || item.TemplateID == TemplateIDs.BranchTemplate)
        if (item == null || !TemplateManager.GetTemplate(item.ID, item.Database).InheritsFrom(new ID("{8CA06D6A-B353-44E8-BC31-B528C7306971}")) || item.TemplateID == TemplateIDs.BranchTemplate) 
        #endregion
        {
          continue;
        }

        var result = HasLink(linkDatabase, item); 

        if (result)
        {
          return true;
        }
      }

      return false;
    }

    private static bool VerifyNoTemplateLinks([NotNull] ClientPipelineArgs args, [CanBeNull] Item item)
    {
      Assert.ArgumentNotNull(args, "args");

      if (item == null)
      {
        return true;
      }

      if (item.TemplateID == TemplateIDs.Template)
      {
        ItemLink[] links = Globals.LinkDatabase.GetReferrers(item);

        if (links.Length > 0 && !IsStandardValuesLink(item, links))
        {
          SheerResponse.Alert(
            Texts.
              THE_0_TEMPLATE_IS_USED_BY_AT_LEAST_ONE_ITEM_PLEASE_DELETE_ALL_THE_ITEMS_THAT_ARE_BASED_ON_THIS_TEMPLATE_FIRST,
            item.DisplayName);
          args.AbortPipeline();
          return false;
        }
      }

      foreach (Item child in item.Children)
      {
        if (!VerifyNoTemplateLinks(args, child))
        {
          return false;
        }
      }

      return true;
    }

    private static bool IsStandardValuesLink([NotNull] Item item, [NotNull] ItemLink[] links)
    {
      Assert.ArgumentNotNull(item, "item");
      Assert.ArgumentNotNull(links, "links");

      if (links.Length != 1)
      {
        return false;
      }

      ItemLink link = links[0];

      TemplateItem template = item;

      Item standardValues = template.StandardValues;

      if (standardValues != null)
      {
        return link.SourceItemID == standardValues.ID || link.TargetItemID == standardValues.ID;
      }

      return false;
    }

    [NotNull]
    private static List<Item> GetItems([NotNull] ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      Database database = GetDatabase(args);

      var result = new List<Item>();

      var list = new ListString(args.Parameters["items"], '|');

      foreach (string id in list)
      {
        Item item = database.GetItem(id, Language.Parse(args.Parameters["language"]));

        if (item != null)
        {
          result.Add(item);
        }
      }

      return Assert.ResultNotNull(result);
    }

    private static Database GetDatabase([NotNull] ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      Database database = Factory.GetDatabase(args.Parameters["database"]);

      Assert.IsNotNull(database, typeof(Database), "Name: {0}", args.Parameters["database"]);

      return Assert.ResultNotNull(database);
    }
  }
}