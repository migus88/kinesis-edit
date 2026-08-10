using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Transfer;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The editor's multi-profile state on its own — <b>no editor, no drive and no
    /// <c>app_settings.txt</c></b>. That is the point of the type existing: every claim about which
    /// profiles a press of Save writes, and about the wording it writes them under, used to have to
    /// be made through a loaded <c>KeyboardEditorViewModel</c> with several staged sessions.
    /// <para>
    /// The write set is what this suite is mostly about (docs/app/keyboard-editor.md, invariant 31):
    /// <b>dirty ∪ renamed, in file order, never the whole cache and never a fallback to the open
    /// profile</b> — and validated in full before a single <c>Save()</c> is called.
    /// </para>
    /// </summary>
    public class ProfileSessionCacheTests
    {
        [AvaloniaFact]
        public void Add_FilesTheSessionUnderItsOwnProfileNumber()
        {
            var cache = new ProfileSessionCache();
            var session = CreateSession(3);

            cache.Add(session);

            Assert.Same(session, cache.Find(3));

            // A profile nobody opened has no session at all, which is what makes "never rewritten"
            // true by construction rather than by a guard.
            Assert.Null(cache.Find(4));
        }

        [AvaloniaFact]
        public void Add_WithNoSession_IsANoOp()
        {
            // Apply hands over whatever the load produced, and a failed load produces nothing.
            var cache = new ProfileSessionCache();

            cache.Add(null);

            Assert.Null(cache.Find(1));
        }

        [AvaloniaFact]
        public void Add_OfAProfileAlreadyHeld_ReplacesItRatherThanThrowing()
        {
            // Apply files the open session on every load, so re-filing has to be a no-op-shaped
            // write: a discard, for instance, applies the same profile number twice.
            var cache = new ProfileSessionCache();
            var first = CreateSession(1);
            var second = CreateSession(1);

            cache.Add(first);
            cache.Add(second);

            Assert.Same(second, cache.Find(1));
        }

        [AvaloniaFact]
        public void AnyIsDirty_AsksTheOpenSessionFirst_AndThenTheRest()
        {
            var cache = new ProfileSessionCache();
            var open = CreateSession(1);
            var other = CreateSession(3);

            cache.Add(open);
            cache.Add(other);

            Assert.False(cache.AnyIsDirty(open));

            // Invariant 30's other half: a profile the user edited and walked away from still says
            // so, because the switch kept it.
            other.IsDirty = true;

            Assert.True(cache.AnyIsDirty(open));

            other.IsDirty = false;
            open.IsDirty = true;

            Assert.True(cache.AnyIsDirty(open));
        }

        [AvaloniaFact]
        public void AnyIsDirty_WithNoOpenSession_StillReadsTheCache()
        {
            // A load that failed leaves the editor with no open session while the profiles visited
            // before it are still held.
            var cache = new ProfileSessionCache();
            var held = CreateSession(2);

            cache.Add(held);

            Assert.False(cache.AnyIsDirty(null));

            held.IsDirty = true;

            Assert.True(cache.AnyIsDirty(null));
        }

        [AvaloniaFact]
        public void TheWriteSet_IsEveryDirtyProfileInFileOrder_AndNothingElse()
        {
            var cache = new ProfileSessionCache();
            var first = CreateSession(1);
            var third = CreateSession(3);
            var fifth = CreateSession(5);

            cache.Add(fifth);
            cache.Add(first);
            cache.Add(third);

            fifth.IsDirty = true;
            first.IsDirty = true;

            var pending = cache.CollectSessionsToSave(new HashSet<int>());

            // In file order however the cache happened to fill, and the clean profile 3 is absent:
            // a v-Drive is flash, and rewriting identical bytes is what this refuses.
            Assert.Equal(new IProfileSession[] { first, fifth }, pending);
        }

        [AvaloniaFact]
        public void TheWriteSet_WithNothingChangedAnywhere_IsEmptyRatherThanTheOpenProfile()
        {
            // The fallback that would look harmless and is the defect: with nothing changed, no file
            // is written at all — the editor says so in a toast instead.
            var cache = new ProfileSessionCache();

            cache.Add(CreateSession(1));
            cache.Add(CreateSession(2));

            Assert.Empty(cache.CollectSessionsToSave(new HashSet<int>()));
        }

        [AvaloniaFact]
        public void TheWriteSet_IncludesAProfileWhoseOnlyChangeIsAMacroRename()
        {
            // A name rides app_settings.txt, so the session re-serializes identically and reports
            // itself clean. Without the renamed set the write set would be empty, the press would
            // report nothing to save, and the name would sit in memory until the editor closed.
            var cache = new ProfileSessionCache();
            var renamed = CreateSession(2);

            cache.Add(CreateSession(1));
            cache.Add(renamed);

            Assert.False(renamed.IsDirty);

            Assert.Same(renamed, Assert.Single(cache.CollectSessionsToSave(new HashSet<int> { 2 })));
        }

        [AvaloniaFact]
        public void TheWriteSet_NeverHoldsAProfileThatCannotBeWritten()
        {
            // The Advantage 360's factory profile. It cannot be reached from the picker, but a
            // write set that could contain it would be one guard away from a mid-loop throw.
            var cache = new ProfileSessionCache();
            var readOnly = CreateSession(0);
            var editable = CreateSession(1);

            readOnly.CanSave = false;
            readOnly.IsDirty = true;
            editable.IsDirty = true;

            cache.Add(readOnly);
            cache.Add(editable);

            Assert.Same(editable, Assert.Single(cache.CollectSessionsToSave(new HashSet<int> { 0, 1 })));
        }

        [AvaloniaFact]
        public void ThePrePass_NamesEveryOffendingProfile_AndHasWrittenNothing()
        {
            // Invariant 31: validation of a multi-profile set is all-or-nothing, so it reports the
            // whole set's violations rather than stopping at the first — and it is a separate call
            // from SaveAll, which is what makes "nothing is written until all of them validate"
            // structural rather than a matter of loop order.
            var third = CreateOverBudgetSession(3);
            var fifth = CreateOverBudgetSession(5);
            var sessions = new IProfileSession[] { CreateSession(1), third, fifth };

            var message = ProfileSessionCache.BuildPreflightViolationMessage(sessions);

            Assert.NotNull(message);
            Assert.Contains(KeyboardEditorViewModel.SaveRejectedProfilesMessage, message, StringComparison.Ordinal);
            Assert.Contains(KeyboardEditorViewModel.BuildProfileCaption(3), message, StringComparison.Ordinal);
            Assert.Contains(KeyboardEditorViewModel.BuildProfileCaption(5), message, StringComparison.Ordinal);

            Assert.Equal(0, third.SaveCallCount);
            Assert.Equal(0, fifth.SaveCallCount);
        }

        [AvaloniaFact]
        public void ThePrePass_WithEveryProfileValid_StopsNothing()
        {
            var sessions = new IProfileSession[] { CreateSession(1), CreateSession(3) };

            Assert.Null(ProfileSessionCache.BuildPreflightViolationMessage(sessions));
        }

        [AvaloniaFact]
        public void ThePrePass_WithOneProfileInTheSet_AddsNoGateOfItsOwn()
        {
            // Deliberate, and not an optimization: with one file there is nothing to be atomic
            // about and Core's own gate already writes nothing, so a second gate here would be a
            // second policy over one question.
            var sessions = new IProfileSession[] { CreateOverBudgetSession(1) };

            Assert.Null(ProfileSessionCache.BuildPreflightViolationMessage(sessions));
        }

        [AvaloniaFact]
        public void SaveAll_RunsOneSavePerProfileInOrder_AndPairsEachResultWithItsNumber()
        {
            var first = CreateSession(1);
            var third = CreateSession(3);

            third.ResultToReturn = new ProfileSaveResult { Success = true, Violations = [], PostSaveMessage = "Press SmartSet + 3." };

            var outcomes = ProfileSessionCache.SaveAll([first, third]);

            Assert.Equal(1, first.SaveCallCount);
            Assert.Equal(1, third.SaveCallCount);
            Assert.Equal(new[] { 1, 3 }, outcomes.Select(outcome => outcome.ProfileNumber));
            Assert.Equal("Press SmartSet + 3.", outcomes[1].Result.PostSaveMessage);
        }

        [AvaloniaFact]
        public void TheRejectionReport_ForOneProfile_KeepsCoresWordingWithNoProfilePrefix()
        {
            // The common case must not change wording just because the mechanism grew.
            var outcomes = new[] { new ProfileSaveOutcome(1, Rejected("the device allows 100")) };

            var message = ProfileSessionCache.BuildRejectedSaveMessage(outcomes);

            Assert.NotNull(message);
            Assert.Contains(KeyboardEditorViewModel.SaveRejectedMessage, message, StringComparison.Ordinal);
            Assert.DoesNotContain(KeyboardEditorViewModel.SaveRejectedProfilesMessage, message, StringComparison.Ordinal);
            Assert.DoesNotContain(KeyboardEditorViewModel.BuildProfileCaption(1), message, StringComparison.Ordinal);
            Assert.Contains("the device allows 100", message, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void TheRejectionReport_ForSeveralProfiles_NamesTheOneThatWasRefused()
        {
            var outcomes = new[]
            {
                new ProfileSaveOutcome(1, FakeProfileSession.SuccessfulSave),
                new ProfileSaveOutcome(3, Rejected("the device allows 100"))
            };

            var message = ProfileSessionCache.BuildRejectedSaveMessage(outcomes);

            Assert.NotNull(message);
            Assert.Contains(KeyboardEditorViewModel.SaveRejectedProfilesMessage, message, StringComparison.Ordinal);
            Assert.Contains(KeyboardEditorViewModel.BuildProfileCaption(3) + ": the device allows 100", message, StringComparison.Ordinal);
            Assert.DoesNotContain(KeyboardEditorViewModel.BuildProfileCaption(1), message, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void TheRejectionReport_WhenEveryWriteLanded_IsNull()
        {
            var outcomes = new[]
            {
                new ProfileSaveOutcome(1, FakeProfileSession.SuccessfulSave),
                new ProfileSaveOutcome(3, FakeProfileSession.SuccessfulSave)
            };

            Assert.Null(ProfileSessionCache.BuildRejectedSaveMessage(outcomes));
        }

        [AvaloniaFact]
        public void TheSuccessSentence_ForOneProfile_IsCoresOwnWordingVerbatim()
        {
            var outcomes = new[] { new ProfileSaveOutcome(1, Saved("Press SmartSet + 1 to load it.")) };

            Assert.Equal("Press SmartSet + 1 to load it.", ProfileSessionCache.BuildSavedProfilesMessage(outcomes));
        }

        [AvaloniaFact]
        public void TheSuccessSentence_ForSeveralProfiles_NamesThemAndKeepsTheDistinctWordings()
        {
            // ProfileSaveMessageCatalog is per profile number, so nine profiles would otherwise be
            // nine toasts; identical wordings collapse, and a distinct one is kept because it is the
            // instruction that gets the profile onto the board.
            var outcomes = new[]
            {
                new ProfileSaveOutcome(1, Saved("Tap SmartSet.")),
                new ProfileSaveOutcome(3, Saved("Tap SmartSet.")),
                new ProfileSaveOutcome(5, Saved("Hold SmartSet + 5."))
            };

            var message = ProfileSessionCache.BuildSavedProfilesMessage(outcomes);

            Assert.Equal("Saved profiles 1, 3 and 5. Tap SmartSet. Hold SmartSet + 5.", message);
        }

        [AvaloniaFact]
        public void TheSuccessSentence_WithNothingSaved_IsNull()
        {
            Assert.Null(ProfileSessionCache.BuildSavedProfilesMessage([]));
        }

        [AvaloniaTheory]
        [InlineData(new[] { 1, 3 }, "Saved profiles 1 and 3.")]
        [InlineData(new[] { 1, 3, 5 }, "Saved profiles 1, 3 and 5.")]
        [InlineData(new[] { 1, 2, 3, 9 }, "Saved profiles 1, 2, 3 and 9.")]
        public void TheProfileList_ReadsAsTheAppsOwnSentence(int[] numbers, string expected)
        {
            Assert.Equal(expected, ProfileSessionCache.BuildSavedProfilesSentence(numbers));
        }

        [AvaloniaFact]
        public void Dispose_ReleasesEveryProfileTheEditorOpened()
        {
            // Since #133 a switch keeps them all, so this is the only path that lets a session go.
            var cache = new ProfileSessionCache();
            var disposable = new DisposableProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb)) { ProfileNumber = 2 };

            cache.Add(CreateSession(1));
            cache.Add(disposable);

            cache.Dispose();

            Assert.True(disposable.IsDisposed);
            Assert.Null(cache.Find(1));
            Assert.Null(cache.Find(2));
        }

        private static FakeProfileSession CreateSession(int profileNumber)
        {
            return new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ProfileNumber = profileNumber
            };
        }

        /// <summary>
        /// A session whose layout is over the device's macro budget, so
        /// <see cref="KeyboardLayout.Validate"/> — the very call <c>ProfileSession.Save</c> makes —
        /// reports a violation. Staged in the model rather than in a canned result, because the
        /// pre-pass asks the layout and never the session.
        /// </summary>
        private static FakeProfileSession CreateOverBudgetSession(int profileNumber)
        {
            var session = CreateSession(profileNumber);
            var limit = session.Layout.Device.Macros.MaxMacroCount
                        ?? throw new InvalidOperationException("The Freestyle Edge RGB is expected to cap its macro count.");

            TestLayouts.FillMacroSlots(session.Layout, limit + 1);

            return session;
        }

        private static ProfileSaveResult Rejected(string message)
        {
            return new ProfileSaveResult
            {
                Success = false,
                Violations =
                [
                    new ModelViolation
                    {
                        Kind = ModelViolationKind.MacroCountExceeded,
                        Message = message
                    }
                ]
            };
        }

        private static ProfileSaveResult Saved(string postSaveMessage)
        {
            return new ProfileSaveResult { Success = true, Violations = [], PostSaveMessage = postSaveMessage };
        }

        /// <summary>
        /// <see cref="IProfileSession"/> is not <see cref="IDisposable"/> today, so the cache's
        /// release is a guard rather than a call — this is what proves the guard fires when a
        /// session does happen to be disposable. It is its own implementation rather than a
        /// subclass because <see cref="FakeProfileSession"/> is sealed, and it answers nothing this
        /// suite asks of it beyond its number.
        /// </summary>
        private sealed class DisposableProfileSession : IProfileSession, IDisposable
        {
            public KeyboardLayout Layout { get; }

            public object? Lighting => null;

            public IReadOnlyList<LayoutInvalidLine> InvalidLines => [];

            public int ProfileNumber { get; init; }

            public bool CanSave => true;

            public bool IsDirty => false;

            public bool IsDisposed { get; private set; }

            public DisposableProfileSession(KeyboardLayout layout)
            {
                Layout = layout;
            }

            public ProfileSaveResult Save()
            {
                throw new NotSupportedException();
            }

            public ProfileImportResult Import(ImportedFileKind kind, IReadOnlyList<string> lines)
            {
                throw new NotSupportedException();
            }

            public void RevertLayout()
            {
                throw new NotSupportedException();
            }

            public void RevertLighting()
            {
                throw new NotSupportedException();
            }

            public IReadOnlyList<ExportFile> PlanExport(ProfileExportSelection selection)
            {
                throw new NotSupportedException();
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
