using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Worker;
using Moq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Worker
{
    public sealed class StepsRunnerL0
    {
        private Mock<IExecutionContext> _ec;
        private StepsRunner _stepsRunner;
        private Variables _variables;

        private TestHostContext CreateTestContext([CallerMemberName] String testName = "")
        {
            var hc = new TestHostContext(this, testName);
            List<string> warnings;
            _variables = new Variables(
                hostContext: hc,
                copy: new Dictionary<string, string>(),
                maskHints: new List<MaskHint>(),
                warnings: out warnings);
            _ec = new Mock<IExecutionContext>();
            _ec.SetupAllProperties();
            _ec.Setup(x => x.Variables).Returns(_variables);
            _stepsRunner = new StepsRunner();
            _stepsRunner.Initialize(hc);
            return hc;
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async Task RunsAfterContinueOnError()
        {
            using (TestHostContext hc = CreateTestContext())
            {
                // Arrange.
                var variableSets = new[]
                {
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true), CreateStep(TaskResult.Succeeded) },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()") },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true), CreateStep(TaskResult.Succeeded, continueOnError: true) },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true), CreateStep(TaskResult.Succeeded, critical: true) },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true), CreateStep(TaskResult.Succeeded, isFinally: true) },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true, critical: true), CreateStep(TaskResult.Succeeded) },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true, critical: true), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()") },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true, critical: true), CreateStep(TaskResult.Succeeded, continueOnError: true) },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true, critical: true), CreateStep(TaskResult.Succeeded, critical: true) },
                    new[] { CreateStep(TaskResult.Failed, continueOnError: true, critical: true), CreateStep(TaskResult.Succeeded, isFinally: true) },
                };
                foreach (var variableSet in variableSets)
                {
                    _ec.Object.Result = null;

                    // Act.
                    await _stepsRunner.RunAsync(
                        jobContext: _ec.Object,
                        steps: variableSet.Select(x => x.Object).ToList());

                    // Assert.
                    Assert.Equal(TaskResult.SucceededWithIssues, _ec.Object.Result);
                    Assert.Equal(2, variableSet.Length);
                    variableSet[0].Verify(x => x.RunAsync());
                    variableSet[1].Verify(x => x.RunAsync());
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async Task RunsAlwaysRuns()
        {
            using (TestHostContext hc = CreateTestContext())
            {
                // Arrange.
                var variableSets = new[]
                {
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Succeeded), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()") },
                        Expected = TaskResult.Succeeded,
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()") },
                        Expected = TaskResult.Failed,
                    },
                };
                foreach (var variableSet in variableSets)
                {
                    _ec.Object.Result = null;

                    // Act.
                    await _stepsRunner.RunAsync(
                        jobContext: _ec.Object,
                        steps: variableSet.Steps.Select(x => x.Object).ToList());

                    // Assert.
                    Assert.Equal(variableSet.Expected, _ec.Object.Result ?? TaskResult.Succeeded);
                    Assert.Equal(2, variableSet.Steps.Length);
                    variableSet.Steps[0].Verify(x => x.RunAsync());
                    variableSet.Steps[1].Verify(x => x.RunAsync());
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async Task RunsFinally()
        {
            using (TestHostContext hc = CreateTestContext())
            {
                // Arrange.
                var variableSets = new[]
                {
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Succeeded), CreateStep(TaskResult.Succeeded, isFinally: true) },
                        Expected = TaskResult.Succeeded,
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded, isFinally: true) },
                        Expected = TaskResult.Failed,
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, isFinally: true) },
                        Expected = TaskResult.Failed,
                    },
                };
                foreach (var variableSet in variableSets)
                {
                    _ec.Object.Result = null;

                    // Act.
                    await _stepsRunner.RunAsync(
                        jobContext: _ec.Object,
                        steps: variableSet.Steps.Select(x => x.Object).ToList());

                    // Assert.
                    Assert.Equal(variableSet.Expected, _ec.Object.Result ?? TaskResult.Succeeded);
                    Assert.Equal(2, variableSet.Steps.Length);
                    variableSet.Steps[0].Verify(x => x.RunAsync());
                    variableSet.Steps[1].Verify(x => x.RunAsync());
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async Task SetsJobResultCorrectly()
        {
            using (TestHostContext hc = CreateTestContext())
            {
                // Arrange.
                var variableSets = new[]
                {
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Abandoned) },
                        Expected = TaskResult.Succeeded
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Canceled) },
                        Expected = TaskResult.Succeeded
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded) },
                        Expected = TaskResult.Failed
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()") },
                        Expected = TaskResult.Failed
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded, isFinally: true) },
                        Expected = TaskResult.Failed
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed, continueOnError: true), CreateStep(TaskResult.Failed) },
                        Expected = TaskResult.Failed
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed, continueOnError: true), CreateStep(TaskResult.Succeeded) },
                        Expected = TaskResult.SucceededWithIssues
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Failed, continueOnError: true, critical: true), CreateStep(TaskResult.Succeeded) },
                        Expected = TaskResult.SucceededWithIssues
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Skipped) },
                        Expected = TaskResult.Succeeded
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Succeeded) },
                        Expected = TaskResult.Succeeded
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Succeeded), CreateStep(TaskResult.Failed) },
                        Expected = TaskResult.Failed
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.Succeeded), CreateStep(TaskResult.SucceededWithIssues) },
                        Expected = TaskResult.SucceededWithIssues
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.SucceededWithIssues), CreateStep(TaskResult.Succeeded) },
                        Expected = TaskResult.SucceededWithIssues
                    },
                    new
                    {
                        Steps = new[] { CreateStep(TaskResult.SucceededWithIssues), CreateStep(TaskResult.Failed) },
                        Expected = TaskResult.Failed
                    },
                //  Abandoned
                //  Canceled
                //  Failed
                //  Skipped
                //  Succeeded
                //  SucceededWithIssues
                };
                foreach (var variableSet in variableSets)
                {
                    _ec.Object.Result = null;

                    // Act.
                    await _stepsRunner.RunAsync(
                        jobContext: _ec.Object,
                        steps: variableSet.Steps.Select(x => x.Object).ToList());

                    // Assert.
                    Assert.True(
                        variableSet.Expected == (_ec.Object.Result ?? TaskResult.Succeeded),
                        $"Expected '{variableSet.Expected}'. Actual '{_ec.Object.Result}'. Steps: {FormatSteps(variableSet.Steps)}");
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async Task SkipsAfterCriticalFailure()
        {
            using (TestHostContext hc = CreateTestContext())
            {
                // Arrange.
                var variableSets = new[]
                {
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded) },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()") },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, continueOnError: true) },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, critical: true) },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()", continueOnError: true, critical: true) },
                };
                foreach (var variableSet in variableSets)
                {
                    _ec.Object.Result = null;

                    Mock<IExecutionContext> stepContext = CreateStepContext();
                    variableSet[1].Setup(x => x.ExecutionContext).Returns(stepContext.Object);

                    // Act.
                    await _stepsRunner.RunAsync(
                        jobContext: _ec.Object,
                        steps: variableSet.Select(x => x.Object).ToList());

                    // Assert.
                    Assert.Equal(TaskResult.Failed, _ec.Object.Result);
                    Assert.Equal(2, variableSet.Length);
                    variableSet[0].Verify(x => x.RunAsync());
                    variableSet[1].Verify(x => x.RunAsync(), Times.Never());
                    stepContext.Verify(x => x.Skip(), Times.Once());
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async Task SkipsAfterFailure()
        {
            using (TestHostContext hc = CreateTestContext())
            {
                // Arrange.
                var variableSets = new[]
                {
                    new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded) },
                    new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded, continueOnError: true) },
                    new[] { CreateStep(TaskResult.Failed), CreateStep(TaskResult.Succeeded, critical: true) },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded) },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, condition: "succeededOrFailed()") },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, continueOnError: true) },
                    new[] { CreateStep(TaskResult.Failed, critical: true), CreateStep(TaskResult.Succeeded, critical: true) },
                };
                foreach (var variableSet in variableSets)
                {
                    _ec.Object.Result = null;

                    Mock<IExecutionContext> stepContext = CreateStepContext();
                    variableSet[1].Setup(x => x.ExecutionContext).Returns(stepContext.Object);

                    // Act.
                    await _stepsRunner.RunAsync(
                        jobContext: _ec.Object,
                        steps: variableSet.Select(x => x.Object).ToList());

                    // Assert.
                    Assert.Equal(TaskResult.Failed, _ec.Object.Result);
                    Assert.Equal(2, variableSet.Length);
                    variableSet[0].Verify(x => x.RunAsync());
                    variableSet[1].Verify(x => x.RunAsync(), Times.Never());
                    stepContext.Verify(x => x.Skip(), Times.Once());
                }
            }
        }

        private Mock<IStep> CreateStep(TaskResult result, string condition = "", Boolean continueOnError = false, Boolean critical = false, Boolean isFinally = false)
        {
            // Setup the step.
            var step = new Mock<IStep>();
            step.Setup(x => x.Condition).Returns(condition);
            step.Setup(x => x.ContinueOnError).Returns(continueOnError);
            step.Setup(x => x.Critical).Returns(critical);
            step.Setup(x => x.Enabled).Returns(true);
            step.Setup(x => x.Finally).Returns(isFinally);
            step.Setup(x => x.RunAsync()).Returns(Task.CompletedTask);

            // Setup the step execution context.
            Mock<IExecutionContext> stepContext = CreateStepContext(result);
            step.Setup(x => x.ExecutionContext).Returns(stepContext.Object);

            return step;
        }

        private Mock<IExecutionContext> CreateStepContext(TaskResult? result = null)
        {
            var stepContext = new Mock<IExecutionContext>();
            stepContext.SetupAllProperties();
            stepContext.Setup(x => x.Variables).Returns(_variables);
            stepContext.Object.Result = result;

            return stepContext;
        }

        private string FormatSteps(IEnumerable<Mock<IStep>> steps)
        {
            return String.Join(
                " ; ",
                steps.Select(x => String.Format(
                    CultureInfo.InvariantCulture,
                    "Returns={0},Condition=[{1}],ContinueOnError={2},Critical={3},Enabled={4},Finally={5}",
                    x.Object.ExecutionContext.Result,
                    x.Object.Condition,
                    x.Object.ContinueOnError,
                    x.Object.Critical,
                    x.Object.Enabled,
                    x.Object.Finally)));
        }
    }
}
