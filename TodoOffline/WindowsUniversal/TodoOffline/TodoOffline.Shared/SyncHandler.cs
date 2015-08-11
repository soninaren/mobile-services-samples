#define Client
//#define Server
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json.Linq;
using Windows.UI.Popups;
using System.Net;

namespace TodoOffline
{
    public class SyncHandler : IMobileServiceSyncHandler
    {
        MobileServiceClient client;
        const string LOCAL_VERSION = "Use local version";
        const string SERVER_VERSION = "Use server version";
        public bool ConflictFound = false;
        private IMobileServiceSyncTable<TodoItem> todoTable;
        public SyncHandler(MobileServiceClient client)
        {
            this.client = client;
            todoTable = App.MobileService.GetSyncTable<TodoItem>();
        }

        public virtual async Task OnPushCompleteAsync(MobileServicePushCompletionResult result)
        {

            foreach (MobileServiceTableOperationError error in result.Errors)
            {
                #region Server wins
#if Server


                #region insert
                // 403
                // Client 1 soft deleted or client 2 network failure // Client 2 inserts
                // resolution Update the local item with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Insert && error.Status == HttpStatusCode.Conflict)
                {

                    // If same preserve server changes 
                    // if different preserver client changes
                    // cannot compare no server item get server item in a seperate call
                    TodoItem clientItem = error.Item.ToObject<TodoItem>();
                    var todoTab = App.MobileService.GetTable<TodoItem>();
                    //TodoItem serverItem;

                    var serverValue = await todoTab.LookupAsync(clientItem.Id);


                    // failed network call so remove update client with server version
                    if (clientItem.Text == serverValue.Text)
                    {
                       await error.CancelAndUpdateItemAsync(error.Result);
                    }
                    // Id collision
                    else
                    {
                        clientItem.Id = null;
                        await todoTable.InsertAsync(clientItem);
                        await error.CancelAndDiscardItemAsync();

                    }
                }
                #endregion

                #region update
                // 412 
                // Client 1 updates // Client 2 tries to update outdated item
                // resolution Update the local item with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Status == HttpStatusCode.PreconditionFailed)
                {
                    await error.CancelAndUpdateItemAsync(error.Result);
                }

                //404
                // Client 1 deletes // Client 2 tries to update deleted item
                // Remove the item from local store
                if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Status == HttpStatusCode.NotFound)
                {
                    await error.CancelAndDiscardItemAsync();
                }
                #endregion

                #region delete

                //412
                // Client 1 updates // Client 2 tries to deletes item
                // Resolution cancel the delete and update the local store with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Delete && error.Status == HttpStatusCode.PreconditionFailed)
                {
                    await error.CancelAndUpdateItemAsync(error.Result);
                }
                #endregion

#endif
                #endregion

                #region ClientWins
#if Client
                TodoItem clientItem = error.Item.ToObject<TodoItem>();
                await error.CancelAndDiscardItemAsync();

                #region insert
                // 403
                // Client 1 soft deleted or client 2 network failure // Client 2 inserts
                // resolution Update the local item with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Insert && error.Status == HttpStatusCode.Conflict)
                {                   
                    var todoTab = App.MobileService.GetTable<TodoItem>();
                    //TodoItem serverItem;

                    var serverValue = await todoTab.LookupAsync(clientItem.Id);

                    // failed network call so remove update client with server version
                    if (clientItem.Text == serverValue.Text)
                    {
                        await error.CancelAndUpdateItemAsync(error.Result);
                    }
                    // Id collision
                    else
                    {
                        clientItem.Id = null;
                        await todoTable.InsertAsync(clientItem);
                        await error.CancelAndDiscardItemAsync();

                    }
                }
                #endregion

                #region update
                // 412 
                // Client 1 updates // Client 2 tries to update outdated item
                // resolution Update the server item
                if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Status == HttpStatusCode.PreconditionFailed)
                {
                    error.Item[MobileServiceSystemColumns.Version] = error.Result[MobileServiceSystemColumns.Version];
                    await todoTable.UpdateAsync(clientItem);
                }

                //404
                // Client 1 deletes // Client 2 tries to update deleted item
                // insert the item to the server with a different id or undelete the item if soft delete is enabled
                if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Status == HttpStatusCode.NotFound)
                {
                    // if soft delete enabled the call undelete and then update
                    await todoTable.InsertAsync(clientItem);
                }
                #endregion

                #region delete

                //412
                // Client 1 updates // Client 2 tries to deletes item
                // Resolution cancel the delete and update the local store with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Delete && error.Status == HttpStatusCode.PreconditionFailed)
                {
                    error.Item[MobileServiceSystemColumns.Version] = error.Result[MobileServiceSystemColumns.Version];
                    await todoTable.DeleteAsync(clientItem);

                }
                #endregion
#endif                
                #endregion                
            }
        }

        public Task<JObject> ExecuteTableOperationAsync(IMobileServiceTableOperation operation)
        {
            Debug.WriteLine("Executing operation '{0}' for table '{1}'", operation.Kind, operation.Table.TableName);
            return operation.ExecuteAsync();
        }

        //public virtual async Task<JObject> ExecuteTableOperationAsync(IMobileServiceTableOperation operation)
        //{
        //    MobileServiceInvalidOperationException error;
        //    Func<Task<JObject>> tryOperation = operation.ExecuteAsync;

        //    do
        //    {
        //        error = null;

        //        try
        //        {
        //            JObject result = await operation.ExecuteAsync();
        //            return result;
        //        }
        //        catch (MobileServiceConflictException ex)
        //        {
        //            error = ex;
        //        }
        //        catch (MobileServicePreconditionFailedException ex)
        //        {
        //            error = ex;
        //        }
        //        catch (Exception e)
        //        {
        //            Debug.WriteLine(e.ToString());
        //            throw e;
        //        }

        //        if (error != null)
        //        {
        //            var localItem = operation.Item.ToObject<TodoItem>();
        //            var serverValue = error.Value;
        //            if (serverValue == null) // 409 doesn't return the server item
        //            {
        //                serverValue = await operation.Table.LookupAsync(localItem.Id) as JObject;
        //            }
        //            var serverItem = serverValue.ToObject<TodoItem>();


        //            if (serverItem.Complete == localItem.Complete && serverItem.Text == localItem.Text)
        //            {
        //                // items are same so we can ignore the conflict
        //                return serverValue;
        //            }

        //            IUICommand command = await ShowConflictDialog(localItem, serverValue);
        //            if (command.Label == LOCAL_VERSION)
        //            {
        //                // Overwrite the server version and try the operation again by continuing the loop
        //                operation.Item[MobileServiceSystemColumns.Version] = serverValue[MobileServiceSystemColumns.Version];
        //                if (error is MobileServiceConflictException) // change operation from Insert to Update
        //                {
        //                    tryOperation = async () => await operation.Table.UpdateAsync(operation.Item) as JObject;
        //                }
        //                continue;
        //            }
        //            else if (command.Label == SERVER_VERSION)
        //            {
        //                return (JObject)serverValue;
        //            }
        //            else
        //            {
        //                operation.AbortPush();
        //            }
        //        }
        //    } while (error != null);

        //    return null;
        //}

        private async Task<IUICommand> ShowConflictDialog(TodoItem localItem, JObject serverValue)
        {
            var dialog = new MessageDialog(
                "How do you want to resolve this conflict?\n\n" + "Local item: \n" + localItem +
                "\n\nServer item:\n" + serverValue.ToObject<TodoItem>(),
                title: "Conflict between local and server versions");

            dialog.Commands.Add(new UICommand(LOCAL_VERSION));
            dialog.Commands.Add(new UICommand(SERVER_VERSION));

            // Windows Phone not supporting 3 command MessageDialog
            //dialog.Commands.Add(new UICommand("Cancel"));

            return await dialog.ShowAsync();
        }
    }
}
