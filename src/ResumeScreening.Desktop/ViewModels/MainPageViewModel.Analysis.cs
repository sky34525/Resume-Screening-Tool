using CommunityToolkit.Mvvm.ComponentModel;
using ResumeScreening.Contracts;
using ResumeScreening_Desktop.Models;

namespace ResumeScreening_Desktop.ViewModels;

public partial class MainPageViewModel
{
    private readonly Dictionary<ResumeItem, ResumeAnalysisDraft> _analysisDrafts = [];

    [ObservableProperty]
    public partial ResumeItem? SelectedResume { get; set; }

    public ResumeAnalysisDraft? GetAnalysisDraft(ResumeItem resume) =>
        _analysisDrafts.TryGetValue(resume, out var draft) ? draft : null;

    public void SetAnalysisDraft(ResumeItem resume, JobScreeningDraft screeningDraft)
    {
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(screeningDraft);

        _analysisDrafts[resume] = new ResumeAnalysisDraft(
            screeningDraft.ResumeAnalysis,
            screeningDraft.SchemaVersion,
            screeningDraft.Model,
            screeningDraft.GeneratedAt,
            screeningDraft.ReviewStatus,
            screeningDraft.ValidationWarnings);
    }
}
