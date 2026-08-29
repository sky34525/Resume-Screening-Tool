using System;
using System.Collections.Generic;
using System.Text;

namespace ResumeScreening.Contracts
{
    public sealed record ResumePage(
        int PageNumber,
        string Text,
        bool IsTextTruncated = false);
}
