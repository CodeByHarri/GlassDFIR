using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using GlassDFIR.Models;

namespace GlassDFIR.Views
{
    public partial class InterestingEntitiesWindow : Window
    {
        public InterestingEntitiesWindow(
            List<EntityCount> ipAddresses,
            List<EntityCount> emailAddresses,
            List<EntityCount> domains,
            List<EntityCount> files)
        {
            InitializeComponent();
            DataContext = new InterestingEntitiesViewModel(ipAddresses, emailAddresses, domains, files);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenValueViewer(sender);
        }

        private void CopyValue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EntityCount entity)
            {
                Clipboard.SetText(entity.Value);
            }
            else
            {
                // Fallback if context menu placement is different or CommandParameter is used
                // Try finding the parent DataGrid and its selected item
                // However, ContextMenu on DataGridRow usually has DataContext as the row item.
                // Let's also check if we can get it from the sender's parent context menu placement target.
                if (sender is MenuItem mi)
                {
                     if (mi.DataContext is EntityCount ec)
                     {
                         Clipboard.SetText(ec.Value);
                     }
                     else if (mi.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement target && target.DataContext is EntityCount ecTarget)
                     {
                         Clipboard.SetText(ecTarget.Value);
                     }
                }
            }
        }

        private void ViewValue_Click(object sender, RoutedEventArgs e)
        {
             if (sender is MenuItem mi)
            {
                // Try all paths to find the object
                EntityCount? entity = mi.DataContext as EntityCount;
                if (entity == null && mi.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement target)
                {
                    entity = target.DataContext as EntityCount;
                }

                if (entity != null)
                {
                     var viewer = new ValueViewer(entity.Value);
                     viewer.Owner = this;
                     viewer.ShowDialog();
                }
            }
        }

        private void OpenValueViewer(object sender)
        {
            if (sender is System.Windows.Controls.DataGrid grid && grid.SelectedItem is EntityCount entity)
            {
                var viewer = new ValueViewer(entity.Value);
                viewer.Owner = this;
                viewer.ShowDialog();
            }
        }
    }

    public class InterestingEntitiesViewModel
    {
        public List<EntityCount> IpAddresses { get; }
        public List<EntityCount> EmailAddresses { get; }
        public List<EntityCount> Domains { get; }
        public List<EntityCount> Files { get; }

        public int IpAddressCount => IpAddresses.Count;
        public int EmailAddressCount => EmailAddresses.Count;
        public int DomainCount => Domains.Count;
        public int FileCount => Files.Count;

        public InterestingEntitiesViewModel(
            List<EntityCount> ipAddresses,
            List<EntityCount> emailAddresses,
            List<EntityCount> domains,
            List<EntityCount> files)
        {
            IpAddresses = ipAddresses;
            EmailAddresses = emailAddresses;
            Domains = domains;
            Files = files;
        }
    }
}
