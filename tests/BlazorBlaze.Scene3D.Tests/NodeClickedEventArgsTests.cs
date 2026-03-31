using BlazorBlaze.Scene3D;
using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D.Tests;

/// <summary>
/// Tests for NodeClickedEventArgs record and ISceneRenderer callback signature.
/// </summary>
public class NodeClickedEventArgsTests
{
    [Fact]
    public void Constructor_WithAllParameters_ShouldPreserveValues()
    {
        // Arrange
        var node = new SceneNode("arm");

        // Act
        var args = new NodeClickedEventArgs(
            NodeName: "arm",
            Node: node,
            Button: 0,
            ClientX: 150.5,
            ClientY: 200.3,
            CtrlKey: true,
            ShiftKey: false,
            AltKey: true,
            WorldPosition: new Point3<double>(10, 20, 30));

        // Assert
        args.NodeName.Should().Be("arm");
        args.Node.Should().BeSameAs(node);
        args.Button.Should().Be(0);
        args.ClientX.Should().Be(150.5);
        args.ClientY.Should().Be(200.3);
        args.CtrlKey.Should().BeTrue();
        args.ShiftKey.Should().BeFalse();
        args.AltKey.Should().BeTrue();
        args.WorldPosition.Should().NotBeNull();
        args.WorldPosition!.Value.X.Should().Be(10);
        args.WorldPosition!.Value.Y.Should().Be(20);
        args.WorldPosition!.Value.Z.Should().Be(30);
    }

    [Fact]
    public void Constructor_BackgroundClick_ShouldHaveNullNodeAndName()
    {
        // Act -- background click: no node hit
        var args = new NodeClickedEventArgs(
            NodeName: null,
            Node: null,
            Button: 0,
            ClientX: 300,
            ClientY: 400,
            CtrlKey: false,
            ShiftKey: false,
            AltKey: false);

        // Assert
        args.NodeName.Should().BeNull();
        args.Node.Should().BeNull();
        args.WorldPosition.Should().BeNull();
    }

    [Fact]
    public void Constructor_WorldPositionDefaults_ToNull()
    {
        // Act -- omit WorldPosition (tests default parameter)
        var args = new NodeClickedEventArgs(
            NodeName: "link1",
            Node: new SceneNode("link1"),
            Button: 2,
            ClientX: 0,
            ClientY: 0,
            CtrlKey: false,
            ShiftKey: false,
            AltKey: false);

        // Assert
        args.WorldPosition.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var node = new SceneNode("test");

        var args1 = new NodeClickedEventArgs("test", node, 0, 100, 200, false, false, false);
        var args2 = new NodeClickedEventArgs("test", node, 0, 100, 200, false, false, false);

        // Assert -- records use value equality for all fields except Node (reference equality)
        args1.Should().Be(args2);
    }

    [Fact]
    public void RecordEquality_DifferentButton_ShouldNotBeEqual()
    {
        // Arrange
        var node = new SceneNode("test");

        var leftClick = new NodeClickedEventArgs("test", node, 0, 100, 200, false, false, false);
        var rightClick = new NodeClickedEventArgs("test", node, 2, 100, 200, false, false, false);

        // Assert
        leftClick.Should().NotBe(rightClick);
    }

    [Fact]
    public void ISceneRenderer_OnNodeClicked_ShouldAcceptNodeClickedEventArgs()
    {
        // Arrange
        var renderer = NSubstitute.Substitute.For<ISceneRenderer>();
        NodeClickedEventArgs? captured = null;

        renderer.OnNodeClicked = async args =>
        {
            captured = args;
            await Task.CompletedTask;
        };

        // Act
        var args = new NodeClickedEventArgs("link1", null, 0, 50, 60, false, true, false);
        renderer.OnNodeClicked!.Invoke(args);

        // Assert
        captured.Should().NotBeNull();
        captured!.NodeName.Should().Be("link1");
        captured.ShiftKey.Should().BeTrue();
    }

    [Fact]
    public void MouseButtons_ShouldFollowJsConvention()
    {
        // JS MouseEvent.button: 0=left, 1=middle, 2=right
        var left = new NodeClickedEventArgs(null, null, 0, 0, 0, false, false, false);
        var middle = new NodeClickedEventArgs(null, null, 1, 0, 0, false, false, false);
        var right = new NodeClickedEventArgs(null, null, 2, 0, 0, false, false, false);

        left.Button.Should().Be(0);
        middle.Button.Should().Be(1);
        right.Button.Should().Be(2);
    }

    [Fact]
    public void With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new NodeClickedEventArgs("arm", null, 0, 100, 200, false, false, false);

        // Act -- use record 'with' expression to create modified copy
        var modified = original with { CtrlKey = true, Button = 2 };

        // Assert
        modified.NodeName.Should().Be("arm");
        modified.CtrlKey.Should().BeTrue();
        modified.Button.Should().Be(2);
        modified.ClientX.Should().Be(100);
        original.CtrlKey.Should().BeFalse(); // original unchanged
    }
}
