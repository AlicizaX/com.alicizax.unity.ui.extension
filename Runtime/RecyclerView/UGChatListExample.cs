using System.Collections.Generic;

namespace AlicizaX.UI
{
    public sealed class ChatMessageData : IMeasuredViewData
    {
        public string Text;
        public float DeclaredLength;

        public bool TryGetItemLength(out float length)
        {
            length = DeclaredLength;
            return DeclaredLength > 0f;
        }
    }

    public sealed class UGChatListExample
    {
        private readonly UGList<ChatMessageData> list;

        public UGChatListExample(RecyclerView recyclerView)
        {
            list = UGListCreateHelper.Create<ChatMessageData>(recyclerView);
            list.ApplyUpdateOptions(new ListUpdateOptions
            {
                ApplyRefreshMode = true,
                RefreshMode = ListRefreshMode.AfterSettle
            });
        }

        public void BindHistory(List<ChatMessageData> history)
        {
            list.SetData(history, ListScrollPolicy.KeepAnchor);
            list.StickToEnd();
        }

        public void Send(ChatMessageData message)
        {
            bool pinToEnd = list.IsAtEnd;
            list.Add(message);
            if (pinToEnd)
            {
                list.StickToEnd();
            }
        }

        public void PrependHistory(IEnumerable<ChatMessageData> older)
        {
            list.InsertRange(0, older);
        }

        public void ResizeItem(int index)
        {
            list.RefreshItem(index, true);
        }

        public void SelectVisible(int dataIndex)
        {
            list.ChoiceIndex = dataIndex;
        }
    }
}
