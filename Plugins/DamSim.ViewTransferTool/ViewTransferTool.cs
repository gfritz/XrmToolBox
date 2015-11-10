﻿using McTools.Xrm.Connection;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Client.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Metadata;
using MsCrmTools.ViewLayoutReplicator.Forms;
using MsCrmTools.ViewLayoutReplicator.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using Tanguy.WinForm.Utilities.DelegatesHelpers;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using CrmExceptionHelper = XrmToolBox.CrmExceptionHelper;

namespace DamSim.ViewTransferTool
{
    public partial class ViewTransferTool : UserControl, IXrmToolBoxPluginControl
    {
        #region Variables

        private EntityMetadata _savedQueryMetadata;

        /// <summary>
        /// XML Document that represents customization
        /// </summary>
        private XmlDocument custoDoc;

        /// <summary>
        /// List of entities
        /// </summary>
        private List<EntityMetadata> entitiesCache;

        /// <summary>
        /// Information panel
        /// </summary>
        private Panel informationPanel;

        /// <summary>
        /// Dynamics CRM 2011 organization service
        /// </summary>
        private IOrganizationService service;

        /// <summary>
        /// Dynamics CRM 2011 target organization service
        /// </summary>
        private IOrganizationService targetService;

        /// <summary>
        /// List of views
        /// </summary>
        private Dictionary<Guid, Entity> viewsList;

        #endregion Variables

        public ViewTransferTool()
        {
            InitializeComponent();
        }

        #region XrmToolbox

        public event EventHandler OnCloseTool;

        public event EventHandler OnRequestConnection;

        public Image PluginLogo
        {
            get { return imageList.Images[0]; }
        }

        public Microsoft.Xrm.Sdk.IOrganizationService Service
        {
            get { throw new NotImplementedException(); }
        }

        public void ClosingPlugin(PluginCloseInfo info)
        {
            if (info.FormReason != CloseReason.None ||
                info.ToolBoxReason == ToolBoxCloseReason.CloseAll ||
                info.ToolBoxReason == ToolBoxCloseReason.CloseAllExceptActive)
            {
                return;
            }

            info.Cancel = MessageBox.Show(@"Are you sure you want to close this tab?", @"Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes;
        }

        public void UpdateConnection(Microsoft.Xrm.Sdk.IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object parameter = null)
        {
            if (actionName == "TargetOrganization")
            {
                targetService = newService;
                SetConnectionLabel(connectionDetail, "Target");
                ((OrganizationServiceProxy)((OrganizationService)targetService).InnerService).Timeout = new TimeSpan(
                    0, 1, 0, 0);
            }
            else
            {
                service = newService;
                SetConnectionLabel(connectionDetail, "Source");
                ((OrganizationServiceProxy)((OrganizationService)service).InnerService).Timeout = new TimeSpan(0, 1, 0, 0);
                LoadEntities();
            }
        }

        #endregion XrmToolbox

        public string GetCompany()
        {
            return GetType().GetCompany();
        }

        public string GetMyType()
        {
            return GetType().FullName;
        }

        public string GetVersion()
        {
            return GetType().Assembly.GetName().Version.ToString();
        }

        private void btnSelectTarget_Click(object sender, EventArgs e)
        {
            if (OnRequestConnection != null)
            {
                var args = new RequestConnectionEventArgs { ActionName = "TargetOrganization", Control = this };
                OnRequestConnection(this, args);
            }
        }

        private void SetConnectionLabel(ConnectionDetail detail, string serviceType)
        {
            switch (serviceType)
            {
                case "Source":
                    lbSourceValue.Text = detail.ConnectionName;
                    lbSourceValue.ForeColor = Color.Green;
                    break;

                case "Target":
                    lbTargetValue.Text = detail.ConnectionName;
                    lbTargetValue.ForeColor = Color.Green;
                    break;
            }
        }

        #region FillEntities

        /// <summary>
        /// Fills the entities listview
        /// </summary>
        public void FillEntitiesList()
        {
            try
            {
                ListViewDelegates.ClearItems(lvEntities);

                foreach (EntityMetadata emd in entitiesCache)
                {
                    var item = new ListViewItem { Text = emd.DisplayName.UserLocalizedLabel.Label, Tag = emd.LogicalName };
                    item.SubItems.Add(emd.LogicalName);
                    ListViewDelegates.AddItem(lvEntities, item);
                }
            }
            catch (Exception error)
            {
                string errorMessage = CrmExceptionHelper.GetErrorMessage(error, true);
                CommonDelegates.DisplayMessageBox(ParentForm, errorMessage, "Error", MessageBoxButtons.OK,
                                                  MessageBoxIcon.Error);
            }
        }

        private void BwFillEntitiesDoWork(object sender, DoWorkEventArgs e)
        {
            // Getting saved query entity metadata
            _savedQueryMetadata = MetadataHelper.RetrieveEntity("savedquery", service);

            // Caching entities
            entitiesCache = MetadataHelper.RetrieveEntities(service);

            // Filling entities list
            FillEntitiesList();
        }

        private void BwFillEntitiesRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                string errorMessage = CrmExceptionHelper.GetErrorMessage(e.Error, true);
                CommonDelegates.DisplayMessageBox(ParentForm, errorMessage, "Error", MessageBoxButtons.OK,
                                                  MessageBoxIcon.Error);
            }
            else
            {
                gbEntities.Enabled = true;
                tsbPublishEntity.Enabled = true;
                tsbPublishAll.Enabled = true;
            }

            Controls.Remove(informationPanel);
            CommonDelegates.SetCursor(this, Cursors.Default);
        }

        private void LoadEntities()
        {
            lvEntities.Items.Clear();
            gbEntities.Enabled = false;
            tsbPublishEntity.Enabled = false;
            tsbPublishAll.Enabled = false;

            lvSourceViews.Items.Clear();
            lvSourceViewLayoutPreview.Columns.Clear();

            CommonDelegates.SetCursor(this, Cursors.WaitCursor);

            informationPanel = InformationPanel.GetInformationPanel(this, "Loading entities...", 340, 120);

            var bwFillEntities = new BackgroundWorker();
            bwFillEntities.DoWork += BwFillEntitiesDoWork;
            bwFillEntities.RunWorkerCompleted += BwFillEntitiesRunWorkerCompleted;
            bwFillEntities.RunWorkerAsync();
        }

        private void tsbCloseThisTab_Click(object sender, EventArgs e)
        {
            if (OnCloseTool != null)
            {
                const string message = "Are you sure to exit?";
                if (MessageBox.Show(message, "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                    DialogResult.Yes)
                    OnCloseTool(this, null);
            }
        }

        private void tsbLoadEntities_Click(object sender, EventArgs e)
        {
            if (service == null)
            {
                if (OnRequestConnection != null)
                {
                    var args = new RequestConnectionEventArgs
                    {
                        ActionName = "Load",
                        Control = this
                    };
                    OnRequestConnection(this, args);
                }
                else
                {
                    MessageBox.Show(this, "OnRequestConnection event not registered!", "Error", MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }
            }
            else
            {
                LoadEntities();
            }
        }

        #endregion FillEntities

        #region FillViews

        private void BwFillViewsDoWork(object sender, DoWorkEventArgs e)
        {
            string entityLogicalName = e.Argument.ToString();

            List<Entity> viewsList = ViewHelper.RetrieveViews(entityLogicalName, entitiesCache, service);
            viewsList.AddRange(ViewHelper.RetrieveUserViews(entityLogicalName, entitiesCache, service));

            HashSet<string> viewOwners = new HashSet<string>();

            foreach (Entity view in viewsList)
            {
                var item = new CrmViewListViewItem(view);
                if (item.HasRequiredFields() && ViewPassesFilters(item))
                {
                    // Add view to each list of views (source and target)
                    ListViewItem clonedItem = (ListViewItem)item.Clone();
                    ListViewDelegates.AddItem(lvSourceViews, item);

                    if (view.Contains("iscustomizable") &&
                        ((BooleanManagedProperty)view["iscustomizable"]).Value == false)
                    {
                        clonedItem.ForeColor = Color.Gray;
                        clonedItem.ToolTipText = "This view has not been defined as customizable";
                    }

                    viewOwners.Add(item.ViewOwner.Name);
                }
            }

            var owners = viewOwners.ToArray();
            Array.Sort(owners);
            ComboboxDelegates.AddRange(cboUserFilter, owners);
        }

        private void BwFillViewsRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Cursor = Cursors.Default;
            gbSourceViews.Enabled = true;

            if (e.Error != null)
            {
                MessageBox.Show(this, "An error occured: " + e.Error.Message, "Error", MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }

            if (lvSourceViews.Items.Count == 0)
            {
                MessageBox.Show(this, "This entity does not contain any view", "Warning", MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private void lvEntities_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetFilterControls();
            PopulateSourceViews();
        }

        private void PopulateSourceViews()
        {
            if (lvEntities.SelectedItems.Count > 0)
            {
                string entityLogicalName = lvEntities.SelectedItems[0].Tag.ToString();

                // Reinit other controls
                lvSourceViews.Items.Clear();
                lvSourceViewLayoutPreview.Columns.Clear();

                Cursor = Cursors.WaitCursor;

                // Launch treatment
                var bwFillViews = new BackgroundWorker();
                bwFillViews.DoWork += BwFillViewsDoWork;
                bwFillViews.RunWorkerAsync(entityLogicalName);
                bwFillViews.RunWorkerCompleted += BwFillViewsRunWorkerCompleted;
            }
        }

        #endregion FillViews

        #region FillViewLayoutDetail

        private void BwDisplayViewDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (ListViewDelegates.GetSelectedItems(lvSourceViews).Count() > 1)
                {
                    ColumnHeader header = new ColumnHeader();
                    header.Width = 380;
                    header.Text = "Layout preview cannot be displayed when multiple views are selected.";
                    ListViewDelegates.AddColumn(lvSourceViewLayoutPreview, header);
                }
                else
                {
                    // Gets current view data
                    Entity currentSelectedView = (Entity)ListViewDelegates.GetSelectedItems(lvSourceViews)[0].Tag;
                    string layoutXml = currentSelectedView["layoutxml"].ToString();
                    string fetchXml = currentSelectedView.Contains("fetchxml")
                                          ? currentSelectedView["fetchxml"].ToString()
                                          : string.Empty;
                    string currentEntityDisplayName = ListViewDelegates.GetSelectedItems(lvEntities)[0].Text;

                    EntityMetadata currentEmd =
                        entitiesCache.Find(
                            delegate(EntityMetadata emd)
                            { return emd.DisplayName.UserLocalizedLabel.Label == currentEntityDisplayName; });

                    XmlDocument layoutDoc = new XmlDocument();
                    layoutDoc.LoadXml(layoutXml);

                    EntityMetadata emdWithItems = MetadataHelper.RetrieveEntity(currentEmd.LogicalName, service);

                    ListViewItem item = new ListViewItem();

                    foreach (XmlNode columnNode in layoutDoc.SelectNodes("grid/row/cell"))
                    {
                        ColumnHeader header = new ColumnHeader();

                        header.Text = MetadataHelper.RetrieveAttributeDisplayName(emdWithItems,
                                                                                  columnNode.Attributes["name"].Value,
                                                                                  fetchXml, service);

                        int columnWidth = columnNode.Attributes["width"] == null ? 0 : int.Parse(columnNode.Attributes["width"].Value);

                        header.Width = columnWidth;

                        ListViewDelegates.AddColumn(lvSourceViewLayoutPreview, header);

                        if (string.IsNullOrEmpty(item.Text))
                            item.Text = columnWidth == 0 ? "(undefined)" : (columnWidth + "px");
                        else
                            item.SubItems.Add(columnWidth == 0 ? "(undefined)" : (columnWidth + "px"));
                    }

                    ListViewDelegates.AddItem(lvSourceViewLayoutPreview, item);

                    GroupBoxDelegates.SetEnableState(gbSourceViewLayout, true);
                }
            }
            catch (Exception error)
            {
                CommonDelegates.DisplayMessageBox(ParentForm, "Error while displaying view: " + error.Message, "Error",
                                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BwDisplayViewRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            lvSourceViews.SelectedIndexChanged += lvSourceViews_SelectedIndexChanged;
            lvSourceViews.Enabled = true;
            CommonDelegates.SetCursor(this, Cursors.Default);
        }

        private void lvSourceViews_SelectedIndexChanged(object sender, EventArgs e)
        {
            lvSourceViewLayoutPreview.Columns.Clear();

            if (lvSourceViews.SelectedItems.Count > 0)
            {
                lvSourceViews.SelectedIndexChanged -= lvSourceViews_SelectedIndexChanged;
                lvSourceViewLayoutPreview.Items.Clear();
                lvSourceViews.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var bwDisplayView = new BackgroundWorker();
                bwDisplayView.DoWork += BwDisplayViewDoWork;
                bwDisplayView.RunWorkerCompleted += BwDisplayViewRunWorkerCompleted;
                bwDisplayView.RunWorkerAsync(lvSourceViews.SelectedItems[0].Tag);
            }
        }

        #endregion FillViewLayoutDetail

        #region Transfer views

        private void BwTransferViewsDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                List<Entity> checkedViews = new List<Entity>();

                foreach (ListViewItem item in ListViewDelegates.GetSelectedItems(lvSourceViews))
                {
                    checkedViews.Add((Entity)item.Tag);
                }

                e.Result = ViewHelper.TransferViews(checkedViews, service, targetService, _savedQueryMetadata);
            }
            catch (Exception error)
            {
                CommonDelegates.DisplayMessageBox(ParentForm, error.Message, "Error", MessageBoxButtons.OK,
                                                  MessageBoxIcon.Error);
            }
        }

        private void BwTransferViewsWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CommonDelegates.SetCursor(this, Cursors.Default);

            Controls.Remove(informationPanel);

            if (e.Result == null) return;

            if (((List<Tuple<string, string>>)e.Result).Count > 0)
            {
                var errorDialog = new ErrorList((List<Tuple<string, string>>)e.Result);
                errorDialog.ShowDialog();
            }
            else
            {
                MessageBox.Show("Selected views have been successfully transfered!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void tsbTransferViews_Click(object sender, EventArgs e)
        {
            if (service == null || targetService == null)
            {
                MessageBox.Show("You must select both a source and a target environment.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (lvSourceViews.SelectedItems.Count == 0)
            {
                MessageBox.Show("You must select at least one view to be transfered in the right list.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CommonDelegates.SetCursor(this, Cursors.WaitCursor);
            var bwTransferViews = new BackgroundWorker();
            bwTransferViews.DoWork += BwTransferViewsDoWork;
            bwTransferViews.RunWorkerCompleted += BwTransferViewsWorkerCompleted;
            bwTransferViews.RunWorkerAsync();
        }

        #endregion Transfer views

        #region Publish entity

        private void BwPublishDoWork(object sender, DoWorkEventArgs e)
        {
            EntityMetadata currentEmd =
                entitiesCache.Find(
                    emd => emd.DisplayName.UserLocalizedLabel.Label == e.Argument.ToString());

            var pubRequest = new PublishXmlRequest();
            pubRequest.ParameterXml = string.Format(@"<importexportxml>
                                                           <entities>
                                                              <entity>{0}</entity>
                                                           </entities>
                                                           <nodes/><securityroles/><settings/><workflows/>
                                                        </importexportxml>",
                                                    currentEmd.LogicalName);

            targetService.Execute(pubRequest);
        }

        private void BwPublishRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CommonDelegates.SetCursor(this, Cursors.Default);
            //Cursor = Cursors.Default;

            if (e.Error != null)
            {
                string errorMessage = CrmExceptionHelper.GetErrorMessage(e.Error, false);
                MessageBox.Show(this, errorMessage, "Error", MessageBoxButtons.OK,
                                                  MessageBoxIcon.Error);
            }

            Controls.Remove(informationPanel);

            tsbPublishEntity.Enabled = true;
            tsbPublishAll.Enabled = true;
            tsbLoadEntities.Enabled = true;
        }

        private void tsbPublishEntity_Click(object sender, EventArgs e)
        {
            if (lvEntities.SelectedItems.Count > 0)
            {
                tsbPublishEntity.Enabled = false;
                tsbPublishAll.Enabled = false;
                tsbLoadEntities.Enabled = false;

                CommonDelegates.SetCursor(this, Cursors.WaitCursor);

                informationPanel = InformationPanel.GetInformationPanel(this, "Publishing entity...", 340, 120);

                var bwPublish = new BackgroundWorker();
                bwPublish.DoWork += BwPublishDoWork;
                bwPublish.RunWorkerCompleted += BwPublishRunWorkerCompleted;
                bwPublish.RunWorkerAsync(lvEntities.SelectedItems[0].Text);
            }
        }

        #endregion Publish entity

        #region Publish all

        private void BwPublishAllDoWork(object sender, DoWorkEventArgs e)
        {
            var pubRequest = new PublishAllXmlRequest();
            targetService.Execute(pubRequest);
        }

        private void BwPublishAllRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Cursor = Cursors.Default;

            if (e.Error != null)
            {
                string errorMessage = CrmExceptionHelper.GetErrorMessage(e.Error, false);
                MessageBox.Show(this, errorMessage, "Error", MessageBoxButtons.OK,
                                                  MessageBoxIcon.Error);
            }

            Controls.Remove(informationPanel);

            tsbPublishEntity.Enabled = true;
            tsbPublishAll.Enabled = true;
            tsbLoadEntities.Enabled = true;
        }

        private void tsbPublishAll_Click(object sender, EventArgs e)
        {
            tsbPublishEntity.Enabled = false;
            tsbPublishAll.Enabled = false;
            tsbLoadEntities.Enabled = false;

            Cursor = Cursors.WaitCursor;

            informationPanel = InformationPanel.GetInformationPanel(this, "Publishing all customizations...", 340, 120);

            var bwPublishAll = new BackgroundWorker();
            bwPublishAll.DoWork += BwPublishAllDoWork;
            bwPublishAll.RunWorkerCompleted += BwPublishAllRunWorkerCompleted;
            bwPublishAll.RunWorkerAsync();
        }

        #endregion Publish all

        #region Filters
        
        private void chkShowActiveViews_CheckedChanged(object sender, EventArgs e)
        {
            PopulateSourceViews();
        }

        private void ResetFilterControls()
        {
            chkShowActiveViews.CheckedChanged -= chkShowActiveViews_CheckedChanged;
            CheckBoxDelegates.SetCheckedState(chkShowActiveViews, false);
            chkShowActiveViews.CheckedChanged += chkShowActiveViews_CheckedChanged;

            cboViewClasses.SelectedIndexChanged -= cboViewTypes_SelectedIndexChanged;
            string[] viewTypesList = { "All Views", "System Views", "User Views" };
            if (cboViewClasses.Items.Count == 0)
            {
                ComboboxDelegates.AddRange(cboViewClasses, viewTypesList);
            }
            ComboboxDelegates.SetSelectedIndex(cboViewClasses,  ComboboxDelegates.GetIndexOf(cboViewClasses, "All Views"));
            cboViewClasses.SelectedIndexChanged += cboViewTypes_SelectedIndexChanged;

            cboUserFilter.SelectedIndexChanged -= cboUserFilter_SelectedIndexChanged;
            ComboboxDelegates.ClearItems(cboUserFilter);
            cboUserFilter.SelectedIndexChanged += cboUserFilter_SelectedIndexChanged;
        }

        public bool ViewPassesFilters(CrmViewListViewItem view)
        {
            bool display = true;

            #region Filters

            if (chkShowActiveViews.Checked)
            {
                var viewStateCode = view.CrmViewEntity.GetAttributeValue<OptionSetValue>("statecode").Value;
                if (viewStateCode == ViewHelper.VIEW_STATECODE_INACTIVE)
                {
                    return false;
                }
            }

            var selectedItem = (string)ComboboxDelegates.GetSelectedItem(cboViewClasses);
            if (selectedItem != "All Views")
            {
                if (selectedItem == "User Views" && view.CrmViewEntity.LogicalName != "userquery")
                {
                    return false;
                }
                if (selectedItem == "System Views" && view.CrmViewEntity.LogicalName != "savedquery")
                {
                    return false;
                }
            }

            var selectedUserName = (string)ComboboxDelegates.GetSelectedItem(cboUserFilter);
            if (selectedUserName != null)
            {
                if (view.ViewOwner.Name != selectedUserName)
                {
                    return false;
                }
            }

            #endregion

            return display;
        }

        private void cboViewTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateSourceViews();
        }

        private void cboUserFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateSourceViews();
        }
        
        #endregion
    }

    public class CrmViewListViewItem : ListViewItem
    {
        private Entity entity;
        public Entity CrmViewEntity
        {
            get
            {
                return entity;
            }
            private set
            {
                this.entity = value;
                this.Tag = value;
            }
        }
        public CrmViewType ViewType { get; private set; }

        private EntityReference viewOwner;
        public EntityReference ViewOwner
        {
            get
            {
                if (entity.LogicalName == "userquery")
                {
                    return entity.GetAttributeValue<EntityReference>("ownerid");
                }
                else
                {
                    return entity.GetAttributeValue<EntityReference>("createdby");
                }
            }
            private set
            {
                viewOwner = value;
            }
        }
        public CrmViewListViewItem() { }

        public CrmViewListViewItem(Entity view)
        {
            if (view.LogicalName == "savedquery" || view.LogicalName == "userquery")
            {
                this.CrmViewEntity = view;
                this.ViewType = new CrmViewType(view);
                if (this.HasRequiredFields())
                {
                    GenerateListViewItemForView(this.CrmViewEntity);
                }
            }
            else
            {
                throw new ArgumentException("Argument entity must have LogicalName \"savedquery\" or \"userquery\"");
            }
        }

        private void GenerateListViewItemForView(Entity view)
        {
            // the order the SubItems are added is very important
            this.SubItems.Clear();

            // View Name
            this.Text = view["name"].ToString(); // for first column
            this.Tag = view;

            // View Type
            this.SubItems.Add(ViewType.ViewTypeName);
            this.ImageIndex = ViewType.ImageIndex;

            // View State
            switch (((OptionSetValue)view["statecode"]).Value)
            {
                case ViewHelper.VIEW_STATECODE_ACTIVE:
                    this.SubItems.Add("Active");
                    break;
                case ViewHelper.VIEW_STATECODE_INACTIVE:
                    this.SubItems.Add("Inactive");
                    break;
            }

            // View Owner
            this.SubItems.Add(ViewOwner.Name);
        }

        public bool HasRequiredFields()
        {
            bool display = true;

            if (this.CrmViewEntity.GetAttributeValue<string>("name") == null)
            {
                return false;
            }

            if (this.CrmViewEntity.GetAttributeValue<OptionSetValue>("statecode") == null)
            {
                return false;
            }

            if (this.CrmViewEntity.GetAttributeValue<int?>("querytype") == null)
            {
                return false;
            }

            if (this.CrmViewEntity.LogicalName == "savedquery")
            {
                if (this.CrmViewEntity.GetAttributeValue<bool?>("isdefault") == null
                    || this.CrmViewEntity.GetAttributeValue<EntityReference>("createdby") == null)
                {
                    return false;
                }
            }

            if (this.CrmViewEntity.LogicalName == "userquery")
            {
                if (this.CrmViewEntity.GetAttributeValue<EntityReference>("ownerid") == null)
                {
                    return false;
                }
            }

            if (this.ViewType.ViewTypeName == null)
            {
                return false;
            }

            return display;
        }

        /// <summary>
        /// Contains information about the type of view 
        /// </summary>
        public class CrmViewType
        {
            public string ViewTypeName { get; private set; }
            /// <summary>
            /// CRM type code for this view type
            /// </summary>
            public int ViewTypeCode { get; private set; }
            /// <summary>
            /// Index for the image associated with this view.
            /// </summary>
            public int ImageIndex { get; private set; }

            public CrmViewType(Entity view)
            {
                if (view.LogicalName == "savedquery" || view.LogicalName == "userquery")
                {
                    SetViewTypeDetails(view);
                }
            }

            public bool IsValid()
            {
                return !String.IsNullOrEmpty(ViewTypeName)
                    && !String.IsNullOrWhiteSpace(ViewTypeName)
                    && ViewTypeCode != null
                    && ImageIndex != null;
            }

            private void SetViewTypeDetails(Entity view)
            {
                #region Gestion de l'image associée à la vue

                switch ((int)view["querytype"])
                {
                    case ViewHelper.VIEW_BASIC:
                        {
                            if (view.LogicalName == "savedquery")
                            {
                                if ((bool)view["isdefault"])
                                {
                                    this.ViewTypeName = "Default public view";
                                    this.ImageIndex = 3;
                                }
                                else
                                {
                                    this.ViewTypeName = "Public view";
                                    this.ImageIndex = 0;
                                }
                            }
                            else
                            {
                                this.ViewTypeName = "User view";
                                this.ImageIndex = 6;
                            }
                        }
                        break;

                    case ViewHelper.VIEW_ADVANCEDFIND:
                        {
                            this.ViewTypeName = "Advanced find view";
                            this.ImageIndex = 1;
                        }
                        break;

                    case ViewHelper.VIEW_ASSOCIATED:
                        {
                            this.ViewTypeName = "Associated view";
                            this.ImageIndex = 2;
                        }
                        break;

                    case ViewHelper.VIEW_QUICKFIND:
                        {
                            this.ViewTypeName = "QuickFind view";
                            this.ImageIndex = 5;
                        }
                        break;

                    case ViewHelper.VIEW_SEARCH:
                        {
                            this.ViewTypeName = "Lookup view";
                            this.ImageIndex = 4;
                        }
                        break;

                    default:
                        {
                            // should not get here...
                            break;
                        }
                }

                #endregion Gestion de l'image associée à la vue
            }
        }
    }
}