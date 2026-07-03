using log4net;
using SW2URDF.URDF;
using SW2URDF.URDFExport.CSV;
using SW2URDF.URDFExport.URDFMerge;
using SW2URDF.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace SW2URDF.UI
{
    /// <summary>
    /// Interaction logic for TreeMergeWPF.xaml
    /// </summary>
    public partial class TreeMergeWPF : Window
    {
        private static readonly ILog logger = Logger.GetLogger();

        public event EventHandler<TreeMergedEventArgs> TreeMerged = delegate { };

        private const int MAX_LABEL_CHARACTER_WIDTH = 40;
        private const int MAX_BUTTON_CHARACTER_WIDTH = 20;

        private readonly string CSVFileName;
        private readonly string AssemblyName;

        private readonly URDFTreeCorrespondance TreeCorrespondance;

        private readonly Link ExistingBaseLink;
        private readonly List<Link> LoadedCSVLinks;
        private readonly HashSet<string> LoadedCSVLinkNames;
        private Link SelectedLink;
        public ObservableCollection<KeyValuePair<string, object>> SelectedLinkProperties { get; }

        public TreeMergeWPF(Link existingLink, List<Link> loadedLinks, string csvFileName, string assemblyName)
        {
            DataContext = this;
            Resources["SelectedLinkProperties"] = SelectedLinkProperties;
            Dispatcher.UnhandledException += AppDispatcherUnhandledException;

            CSVFileName = csvFileName;
            AssemblyName = assemblyName;

            InitializeComponent();
            ConfigureLabels();

            ExistingBaseLink = existingLink;
            LoadedCSVLinks = new List<Link>(loadedLinks);
            LoadedCSVLinkNames = new HashSet<string>();
            TreeCorrespondance = new URDFTreeCorrespondance();
            SelectedLinkProperties = new ObservableCollection<KeyValuePair<string, object>>();

            PropertiesListView.DataContext = SelectedLinkProperties;

            ExistingTreeView.SelectedItemChanged += OnTreeItemClick;
            MergeAndUpdate();
        }

        private void AppDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error("Exception encountered in TreeMerge form", e.Exception);
            MessageBox.Show("There was a problem with the TreeMerge form: \n\"" +
                e.Exception.Message + "\"\nEmail your maintainer with the log file found at " +
                Logger.GetFileName());
            e.Handled = true;
        }

        /// <summary>
        /// This method performs a merge between the Loaded links and the existing configuration
        /// based on the names of the loaded links. It also updates the form to match the most
        /// updated merge
        /// </summary>
        /// <returns></returns>
        private TreeMerger MergeAndUpdate()
        {
            // Setup merge to start with a fresh link,
            ExistingTreeView.SetTree(ExistingBaseLink.Clone());
            LoadedCSVLinkNames.Clear();
            LoadedCSVLinkNames.UnionWith(LoadedCSVLinks.Select(link => link.Name));

            // Update correspondance with the most up-to-date names as well as the
            // appropriate list boxes
            TreeCorrespondance.BuildCorrespondance(ExistingTreeView, LoadedCSVLinks,
                out List<Link> matched, out List<Link> unmatched);
            UpdateList(MatchingLoadedLinks, matched);
            UpdateList(UnmatchedLoadedLinks, unmatched);

            // Perform merge
            TreeMerger merger = new TreeMerger(MassInertiaLoadedButton.IsChecked.Value,
                                                        VisualLoadedButton.IsChecked.Value,
                                                        JointKinematicsLoadedButton.IsChecked.Value,
                                                        OtherJointLoadedButton.IsChecked.Value);

            Link mergedRoot = merger.Merge(ExistingTreeView, TreeCorrespondance);

            // Update Form Tree
            ExistingTreeView.SetTree(mergedRoot);

            return merger;
        }

        private void UpdateList(ListBox listBox, List<Link> unmatched)
        {
            listBox.Items.Clear();
            foreach (Link link in unmatched)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Tag = link,
                    Name = link.Name,
                    Content = new TextBlock { Text = link.Name }
                };
                item.Selected += OnListBoxItemClick;
                listBox.Items.Add(item);
            }
        }

        private void CancelClick(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you wish to cancel? Any changes you have made will not be saved",
                "Cancel Merge?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Close();
            }
        }

        private void MergeClick(object sender, EventArgs e)
        {
            TreeMerger merger = MergeAndUpdate();
            if (UnmatchedLoadedLinks.Items.Count > 0)
            {
                IEnumerable<string> unmatchedLinkNames =
                    UnmatchedLoadedLinks.Items
                                        .Cast<ListBoxItem>()
                                        .Select(item => ((Link)item.Tag).Name);

                string unmatchedLinksStr = string.Join("\r\n", unmatchedLinkNames);

                string message = "The follow links loaded from the CSV " + CSVFileName + " have not " +
                    "been matched with links in the assembly configuration, would you like to " +
                    "continue?\r\n\r\n" + unmatchedLinksStr;

                MessageBoxResult result =
                    MessageBox.Show(message, "Merge with unmatched links?", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            TreeMergedEventArgs mergedArgs = new TreeMergedEventArgs(ExistingTreeView, true, merger, CSVFileName);
            TreeMerged(this, mergedArgs);

            Close();
        }

        /// <summary>
        /// When an item in any of the list boxes or treeview is selected, the box and properties list view
        /// need to be populated. The link name TextBox is directly set, while the the properties ListView is
        /// bound to the SelectedLinkProperties dictionary
        /// </summary>
        /// <param name="link"></param>
        private void FillSelectedLinkBoxes(Link link)
        {
            SelectedLinkName.Text = link.Name;

            OrderedDictionary dictionary = new OrderedDictionary();
            link.AppendToCSVDictionary(new List<string>(), dictionary);

            SelectedLinkProperties.Clear();
            foreach (DictionaryEntry entry in dictionary)
            {
                string context = (string)entry.Key;
                string columnName = (string)ContextToColumns.Dictionary[context];
                SelectedLinkProperties.Add(new KeyValuePair<string, object>(columnName, entry.Value));
            }
        }

        private void ClearLinkBoxes()
        {
            SelectedLinkName.Text = null;
            SelectedLinkProperties.Clear();
        }

        private void UnselectOtherBoxes(object boxWithSelection)
        {
            TreeViewItem selectedTreeItem = (ExistingTreeView.SelectedItem as TreeViewItem);
            if (boxWithSelection != ExistingTreeView && selectedTreeItem != null)
            {
                selectedTreeItem.IsSelected = false;
            }

            ListBoxItem previouslySelected = (MatchingLoadedLinks.SelectedItem as ListBoxItem);
            if (boxWithSelection != MatchingLoadedLinks && previouslySelected != null)
            {
                previouslySelected.IsSelected = false;
            }

            previouslySelected = (UnmatchedLoadedLinks.SelectedItem as ListBoxItem);
            if (boxWithSelection != UnmatchedLoadedLinks && previouslySelected != null)
            {
                previouslySelected.IsSelected = false;
            }
        }

        private void OnListBoxItemClick(object sender, RoutedEventArgs e)
        {
            UnselectOtherBoxes(sender);

            ListBoxItem selectedItem = (sender as ListBoxItem);
            Link selectedLink = (Link)selectedItem.Tag;
            ProcessItemClick(sender, selectedLink, true);
        }

        private void OnTreeItemClick(object sender, RoutedEventArgs e)
        {
            TreeView tree = (TreeView)sender;
            if (tree.SelectedItem == null)
            {
                return;
            }

            TreeViewItem selectedItem = (TreeViewItem)tree.SelectedItem;
            Link link = (Link)selectedItem.Tag;

            ProcessItemClick(sender, link, false);
        }

        private void ProcessItemClick(object sender, Link link, bool isLoadedFromCSV)
        {
            UnselectOtherBoxes(sender);
            SelectedLinkName.IsReadOnly = !isLoadedFromCSV;
            SelectedLink = link;
            FillSelectedLinkBoxes(link);

            string labelText = (isLoadedFromCSV) ? "Properties loaded from CSV" : "Preview of Merged Properties";
            PropertiesLoadedLabel.Content = new TextBlock { Text = labelText };
        }

        private void OnUpdateButtonClick(object sender, RoutedEventArgs e)
        {
            if (SelectedLink == null)
                return;

            string updatedName = SelectedLinkName.Text;
            HashSet<string> existingNames = new HashSet<string>(LoadedCSVLinkNames);
            existingNames.Remove(SelectedLink.Name);

            if (string.IsNullOrWhiteSpace(updatedName))
            {
                SelectedLinkName.ToolTip = new ToolTip
                {
                    Content = "Link name cannot be empty",
                    IsOpen = true,
                    StaysOpen = false,
                };
                return;
            }

            if (existingNames.Contains(updatedName))
            {
                SelectedLinkName.ToolTip = new ToolTip
                {
                    Content = "\"" + updatedName + "\" already exists",
                    IsOpen = true,
                    StaysOpen = false,
                };
                return;
            }
            SelectedLinkName.ToolTip = null;
            SelectedLink.Name = updatedName;
            MergeAndUpdate();
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            FillSelectedLinkBoxes(SelectedLink);
        }

        private void OnRadioButtonClick(object sender, RoutedEventArgs e)
        {
            MergeAndUpdate();
            ClearLinkBoxes();
            SelectedLink = null;
        }

        private static string ShortenStringForLabel(string text, int numCharacters)
        {
            string result = text;
            if (text.Length > numCharacters)
            {
                string extension = Path.GetExtension(text);
                int numToKeep = numCharacters - "...".Length - extension.Length;
                result = text.Substring(0, numToKeep) + "..." + extension;
            }
            return result;
        }

        private static TextBlock BuildTextBlock(string boldBit, string regularBit)
        {
            TextBlock block = new TextBlock();
            block.Inlines.Add(new Bold(new Run(boldBit)));
            block.Inlines.Add(regularBit);
            return block;
        }

        private void ConfigureLabels()
        {
            string shortAssemblyName = ShortenStringForLabel(AssemblyName, MAX_BUTTON_CHARACTER_WIDTH);

            string longCSVFilename = ShortenStringForLabel(CSVFileName, MAX_LABEL_CHARACTER_WIDTH);
            string shortCSVFilename = ShortenStringForLabel(CSVFileName, MAX_BUTTON_CHARACTER_WIDTH);

            MatchedListLabel.Content = BuildTextBlock("Matching Links from CSV: ", longCSVFilename);
            ExistingTreeLabel.ToolTip =
                new TextBlock { Text = "Configuration from Assembly: " + AssemblyName };

            MassInertiaExistingButton.Content = new TextBlock { Text = shortAssemblyName };
            MassInertiaExistingButton.ToolTip =
                new TextBlock { Text = "Use Mass and Inertia properties loaded from: " + AssemblyName };

            VisualExistingButton.Content = new TextBlock { Text = shortAssemblyName };
            VisualExistingButton.ToolTip =
                new TextBlock { Text = "Use Mesh and Material properties loaded from: " + AssemblyName };

            JointKinematicsExistingButton.Content = new TextBlock { Text = shortAssemblyName };
            JointKinematicsExistingButton.ToolTip =
                new TextBlock { Text = "Use Joint Kinematic properties loaded from: " + AssemblyName };

            OtherJointExistingButton.Content = new TextBlock { Text = shortAssemblyName };
            OtherJointExistingButton.ToolTip = new TextBlock
            {
                Text =
                "Use Limits, Dynamics, Calibration and Safety Controller values loaded from: " +
                AssemblyName
            };

            MassInertiaLoadedButton.Content = new TextBlock { Text = shortCSVFilename };
            MassInertiaLoadedButton.ToolTip =
                new TextBlock { Text = "Use Mass and Inertia properties loaded from: " + CSVFileName };

            VisualLoadedButton.Content = new TextBlock { Text = shortCSVFilename };
            VisualLoadedButton.ToolTip =
                new TextBlock { Text = "Use Mesh and Material properties loaded from: " + CSVFileName };

            JointKinematicsLoadedButton.Content = new TextBlock { Text = shortCSVFilename };
            JointKinematicsLoadedButton.ToolTip =
                new TextBlock { Text = "Use Joint Kinematic properties loaded from: " + CSVFileName };

            OtherJointLoadedButton.Content = new TextBlock { Text = shortCSVFilename };
            OtherJointLoadedButton.ToolTip = new TextBlock
            {
                Text = "Use Limits, Dynamics, Calibration and Safety Controller values loaded from: " + CSVFileName
            };
        }

        private void TreeViewDrop(object sender, DragEventArgs e)
        {
            URDFTreeView tree = (URDFTreeView)sender;
            TreeViewItem package = e.Data.GetData(typeof(TreeViewItem)) as TreeViewItem;

            if (!tree.IsValidDrop(tree, package, e))
            {
                return;
            }

            if (e.Source.GetType() == typeof(TreeViewItem))
            {
                // Dropping onto a Tree node
                URDFTreeView.MoveTreeItem((TreeViewItem)e.Source, package);
            }
            else if (e.Source.GetType() == typeof(TreeView))
            {
                // Dropping outside of a node will reorder nodes
                ProcessDragDropOnTree(tree, package, e);
            }
        }

        /// <summary>
        /// This is how we control how TreeViewItems are highlighted when someone drags over them
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewItemDragEnter(object sender, DragEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;
            if (ExistingTreeView.IsValidDrop(item, e))
            {
                TreeViewItem target = (TreeViewItem)e.Source;
                target.Background = SystemColors.ActiveBorderBrush;
            }
        }

        /// <summary>
        /// This disables the highlight when dragging over leaves this element
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewItemDragLeave(object sender, DragEventArgs e)
        {
            if (e.Source.GetType() == typeof(TreeViewItem))
            {
                TreeViewItem target = (TreeViewItem)e.Source;
                target.Background = null;
            }
        }

        /// <summary>
        /// If they don't drop the package directly on an item, and instead drop it on the tree
        /// then that's how things get reordered.
        /// </summary>
        private void ProcessDragDropOnTree(URDFTreeView tree, TreeViewItem package, DragEventArgs e)
        {
            TreeViewItem closest = URDFTreeView.GetItemToSideOfPoint(tree, e);

            if (closest == null)
            {
                return;
            }

            if (closest.Items.Count > 0)
            {
                URDFTreeView.MoveTreeItem(closest, package, 0);
            }
            else
            {
                TreeViewItem parent = (TreeViewItem)closest.Parent;
                int closestIndex = parent.Items.IndexOf(closest);
                URDFTreeView.MoveTreeItem(parent, package, closestIndex + 1);
            }
        }

        private TreeViewItem BuildTreeViewItem(LinkNode node)
        {
            TreeViewItem item = new TreeViewItem
            {
                Tag = node.Link,
                IsExpanded = true,
                AllowDrop = true,
                Name = node.Name,
                Header = node.Name,
            };

            item.DragEnter += TreeViewItemDragEnter;
            item.DragLeave += TreeViewItemDragLeave;

            foreach (LinkNode child in node.Nodes)
            {
                item.Items.Add(BuildTreeViewItem(child));
            }

            return item;
        }

        private void MenuClick(object sender, RoutedEventArgs e)
        {
            (sender as Button).ContextMenu.IsEnabled = true;
            (sender as Button).ContextMenu.PlacementTarget = (sender as Button);
            (sender as Button).ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            (sender as Button).ContextMenu.IsOpen = true;
        }

        private void MenuItemChecked(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            logger.Info("Parent type " + menuItem.Parent.GetType());
            ContextMenu contextMenuParent = menuItem.Parent as ContextMenu;
            foreach (MenuItem item in contextMenuParent.Items)
            {
                if (item != sender)
                {
                    logger.Info("Unchecking " + item.Header);
                    item.IsChecked = false;
                }
            }

            // During the InitializeComponents, this callback fires, but things aren't fully setup
            if (!(contextMenuParent.PlacementTarget is Button button))
            {
                return;
            }
            if (!(menuItem.Header is TextBlock menuItemText))
            {
                return;
            }
            button.Content = new TextBlock
            {
                Text = menuItemText.Text,
            };
        }
    }
}