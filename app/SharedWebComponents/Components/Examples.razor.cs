// Copyright (c) Microsoft. All rights reserved.

namespace SharedWebComponents.Components;

public sealed partial class Examples
{
    [Parameter, EditorRequired] public required string Message { get; set; }
    [Parameter, EditorRequired] public EventCallback<string> OnExampleClicked { get; set; }
    [CascadingParameter(Name = "Settings")]
    public RequestSettingsOverrides? Settings { get; set; }

    private string DocStarterQuery1 { get; } = "What is NTT Code of Conduct?";
    private string DocStarterQuery2 { get; } = "Can my annual leave credit be carried forward into the next year?";
    private string DocStarterQuery3 { get; } = "What is the benefit of Workday system?";
    private string DBStarterQuery1 { get; } = "Show me top 5 overdue invoices";
    private string DBStarterQuery2 { get; } = "What is the outstanding balance for Baker-Myers?";
    private string DBStarterQuery3 { get; } = "Count payments by payment method for invoices issued in 2024";

    private async Task OnClickedAsync(string exampleText)
    {
        if (OnExampleClicked.HasDelegate)
        {
            await OnExampleClicked.InvokeAsync(exampleText);
        }
    }
}
