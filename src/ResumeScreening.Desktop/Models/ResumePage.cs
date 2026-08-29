using System;
using System.Collections.Generic;
using System.Text;

namespace ResumeScreening_Desktop.Models
{
    public sealed record ResumePage(
        int PageNumber,
        string Text,
        bool IsTextTruncated = false);
}
