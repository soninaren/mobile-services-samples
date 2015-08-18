using System;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Collections.Generic;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using Microsoft.WindowsAzure.MobileServices.Sync;
namespace TodoOffline
{
    public class MyConflictResolver : ConflictResolver
    {

        override public async Task HandleConflicts(Conflict conflict)
        {
            switch (conflict.conflictType)
            {
                case ConflictType.ServerUpdatedClientUpdated:
                    await ResolveWithServerItem(conflict);
                    break;
                case ConflictType.ServerDeletedClientUpdated:
                    await ResolveWithServerItem(conflict);
                    break;
                case ConflictType.ServerUpdatedClientDeleted:
                    await ResolveWithServerItem(conflict);
                    break;
                case ConflictType.ServerInsertedClientInserted:
                    await ResolveWithItem(conflict, conflict.ServerObject);
                    break;
                default:
                    await ResolveWithServerItem(conflict);
                    break;
            }
        }
    }
}
