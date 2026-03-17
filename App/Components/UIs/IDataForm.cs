namespace App.UIs
{
    /// <summary>
    /// 数据表单接口（支持数据的新建、展示、获取）
    /// </summary>
    public interface IDataForm<T>
    {
        /// <summary>新建数据时调用，可在此方法中清空表单</summary>
        void NewData();

        /// <summary>编辑数据时调用，可在此方法显示数据</summary>
        void ShowData(T item);

        /// <summary>采集表单数据供保存时调用，可在此方法中从表单获取数据</summary>
        void CollectData(ref T item);
    }
}
