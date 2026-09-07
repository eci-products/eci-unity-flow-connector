using EciFlowConnector.Runtime;
using EciFlowConnector.Editor.App;
using EciFlowConnector.Editor.Domain.Entries;
using EciFlowConnector.Editor.Domain.Versioning;
using EciFlowConnector.Editor.Features.EntryDetails;
using EciFlowConnector.Editor.Features.Export;
using EciFlowConnector.Editor.Features.Highlighter;
using EciFlowConnector.Editor.Features.Import;
using EciFlowConnector.Editor.Features.Screenshots;
using EciFlowConnector.Editor.Features.Settings;
using EciFlowConnector.Editor.Features.TableBrowser;
using EciFlowConnector.Editor.Infrastructure.Api;
using EciFlowConnector.Editor.Infrastructure.Api.Contracts;
using EciFlowConnector.Editor.Infrastructure.Images;
using EciFlowConnector.Editor.Infrastructure.Localization;
using EciFlowConnector.Editor.Infrastructure.Persistence;
using EciFlowConnector.Editor.UI.Components.MultiDropdown;
using EciFlowConnector.Editor.UI.Components.ResizablePanel;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EciFlowConnector.Editor.Infrastructure.Api.Contracts
{

public sealed class GamePushParam
{
    public string branchId;
    public VersionSourceTypeEnum versionSourceTypeEnum;
    public TaskTypeEnum taskType;
    public List<GameTableData> tableData;
}

public sealed class GameTableData
{
    public string tableName;
    public List<GameEntryData> entryData;
}

public sealed class GameEntryData
{
    public string entryKey;
    public string versionId;
    public Dictionary<string, string> cellValue;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public byte[] image;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? limitSize;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string comment;
}

public sealed class GamePushDTO
{
    public string newVersion;
    public string screenShotId;
}

public enum TaskTypeEnum
{
    INIT,
    SYNC,
    UPDATE,
    ORDER_DELIVERY,
    LOCALIZATION_EXPORT,
    BRANCH_CREATE,
    CUSTOM_EXPORT
}

public enum VersionSourceTypeEnum
{
    INIT,
    SYNC,
    UPDATE,
    ORDER_DELIVERY,
    BRANCH_CREATE,
    MERGE_BRANCH,
    EDIT_CELL,
    LOCK_ROW,
    UNLOCK_ROW,
    DELETE_ROW,
    CHANGE_SOURCE_STATUS,
    REORDER_ROWS,
    RESTORE_VERSION,
    ADD_ROW
}
}
