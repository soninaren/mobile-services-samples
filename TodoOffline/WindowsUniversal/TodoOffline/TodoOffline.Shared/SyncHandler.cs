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

        public SyncHandler(MobileServiceClient client)
        {
            this.client = client;
        }

        public virtual Task OnPushCompleteAsync(MobileServicePushCompletionResult result)
        {
            // Server wins             
            foreach (MobileServiceTableOperationError error in result.Errors)
            {

                #region insert
                // 403
                // Client 1 soft deleted // Client 2 inserts
                // resolution Update the local item with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Insert && error.Status == HttpStatusCode.Conflict)
                {
                    TodoItem clientItem = error.Item.ToObject<TodoItem>();

                    // cannot compare no server item
                    //TodoItem serverItem = error.Result.ToObject<TodoItem>();                    
                    //if (clientItem.Text == serverItem.Text)
                    //{
                    //    error.CancelAndUpdateItemAsync(error.Result);
                    //}
                    //else
                    //{
                    error.CancelAndDiscardItemAsync();

                    // how to insert a new record, this shows there a dialogbox with text were no errors but push failed.
                    //IMobileServiceSyncTable<TodoItem> todoTable = App.MobileService.GetSyncTable<TodoItem>();                                        
                    //clientItem.Id = null;
                    //todoTable.InsertAsync(clientItem);
                    //App.MobileService.SyncContext.PushAsync();

                    //}
                }
                #endregion                

                #region update                
                // 412 
                // Client 1 updates // Client 2 tries to update outdated item
                // resolution Update the local item with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Status == HttpStatusCode.PreconditionFailed)
                {
                    error.CancelAndUpdateItemAsync(error.Result);
                }

                //404
                // Client 1 deletes // Client 2 tries to update deleted item
                // Remove the item from local store
                if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Status == HttpStatusCode.NotFound)
                {
                    error.CancelAndDiscardItemAsync();
                }
                #endregion                

                #region delete
                
                //412
                // Client 1 updates // Client 2 tries to deletes item
                // Resolution cancel the delete and update the local store with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Delete && error.Status == HttpStatusCode.PreconditionFailed)
                {
                    error.CancelAndUpdateItemAsync(error.Result);
                }

                //404
                // Client 1 updates // Client 2 tries to deletes item
                // Resolution cancel the delete and update the local store with server value
                if (error.OperationKind == MobileServiceTableOperationKind.Delete && error.Status == HttpStatusCode.NotFound)
                {
                    error.CancelAndDiscardItemAsync();
                }

                #endregion
            }
            return Task.FromResult(0);
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
