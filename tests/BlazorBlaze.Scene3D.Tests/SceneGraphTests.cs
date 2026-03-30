using BlazorBlaze.Scene3D;
using BlazorBlaze.Scene3D.Geometries;
using ModelingEvolution.Drawing;

namespace BlazorBlaze.Scene3D.Tests;

/// <summary>
/// Test group 10: Scene3D tests (3.1-3.6) from test-scenarios.md.
/// </summary>
public class SceneGraphTests
{
    /// <summary>
    /// Test 3.1 -- Scene graph add, find, and remove nodes.
    /// </summary>
    [Fact]
    public void AddFindRemove_Should_ManageNodesCorrectly()
    {
        // Arrange
        var graph = new SceneGraph();

        // Act -- add root and children
        var robot = graph.AddRoot("robot");
        var link1 = robot.AddChild("link1");
        var link2 = robot.AddChild("link2");

        // Assert -- find nodes
        graph.Find("robot").Should().BeSameAs(robot);
        var foundLink1 = graph.Find("link1");
        foundLink1.Should().NotBeNull();
        foundLink1!.Parent.Should().BeSameAs(robot);
        robot.Children.Count.Should().Be(2);

        // Act -- chain of 5 nested nodes: root -> n1 -> n2 -> n3 -> n4
        var chainRoot = graph.AddRoot("chainRoot");
        var n1 = chainRoot.AddChild("n1");
        var n2 = n1.AddChild("n2");
        var n3 = n2.AddChild("n3");
        var n4 = n3.AddChild("n4");

        // Assert -- find n4 and traverse up
        graph.Find("n4").Should().NotBeNull();
        n4.Depth().Should().Be(5); // chainRoot(1) -> n1(2) -> n2(3) -> n3(4) -> n4(5)

        // Act -- remove link1
        robot.RemoveChild("link1");

        // Assert -- link1 gone, link2 still there
        graph.Find("link1").Should().BeNull();
        robot.Children.Count.Should().Be(1);
        robot.Children[0].Name.Should().Be("link2");
    }

    /// <summary>
    /// Test 3.2 -- Scene graph node geometry types.
    /// </summary>
    [Fact]
    public void GeometryTypes_Should_BeCorrect()
    {
        // Arrange
        var graph = new SceneGraph();
        var root = graph.AddRoot("root");

        // Act -- create nodes with each geometry type
        var cylinder = root.AddChild("cylinder");
        cylinder.Geometry = new CylinderGeometry(10, 100);

        var sphere = root.AddChild("sphere");
        sphere.Geometry = new SphereGeometry(5);

        var line = root.AddChild("line");
        line.Geometry = new LineGeometry(Point3<double>.Zero, new Point3<double>(100, 0, 0));

        var grid = root.AddChild("grid");
        grid.Geometry = new GridGeometry(1000, 50);

        var frustum = root.AddChild("frustum");
        frustum.Geometry = new FrustumGeometry(60, 1.5, 1, 1000);

        var textLabel = root.AddChild("textLabel");
        textLabel.Geometry = new TextLabelGeometry("Hello", 12);

        var coordAxes = root.AddChild("coordAxes");
        coordAxes.Geometry = new CoordinateAxesGeometry(100);

        var coordOverlay = root.AddChild("coordOverlay");
        coordOverlay.Geometry = new CoordinateSystemOverlayGeometry(50);

        // Assert -- each geometry type is correct
        cylinder.Geometry.Should().BeOfType<CylinderGeometry>();
        ((CylinderGeometry)cylinder.Geometry!).Radius.Should().Be(10);
        ((CylinderGeometry)cylinder.Geometry!).Height.Should().Be(100);

        sphere.Geometry.Should().BeOfType<SphereGeometry>();
        ((SphereGeometry)sphere.Geometry!).Radius.Should().Be(5);

        line.Geometry.Should().BeOfType<LineGeometry>();
        grid.Geometry.Should().BeOfType<GridGeometry>();
        frustum.Geometry.Should().BeOfType<FrustumGeometry>();
        textLabel.Geometry.Should().BeOfType<TextLabelGeometry>();
        coordAxes.Geometry.Should().BeOfType<CoordinateAxesGeometry>();
        coordOverlay.Geometry.Should().BeOfType<CoordinateSystemOverlayGeometry>();

        // Assert -- total count is 8 geometry types
        root.Children.Count.Should().Be(8);
    }

    /// <summary>
    /// Test 3.3 -- Scene graph rejects duplicate node names under same parent.
    /// </summary>
    [Fact]
    public void DuplicateNames_Should_ThrowUnderSameParent()
    {
        // Arrange
        var graph = new SceneGraph();
        var robot = graph.AddRoot("robot");
        robot.AddChild("link1");

        // Act & Assert -- duplicate under same parent throws
        var act = () => robot.AddChild("link1");
        act.Should().Throw<ArgumentException>();

        // Act -- same name under different parent succeeds
        var arm = graph.AddRoot("arm");
        var addUnderDifferentParent = () => arm.AddChild("link1");
        addUnderDifferentParent.Should().NotThrow();
    }

    /// <summary>
    /// Test 3.4 -- Scene graph JSON serialization round-trip.
    /// </summary>
    [Fact]
    public void JsonRoundTrip_Should_PreserveStructure()
    {
        // Arrange
        var graph = new SceneGraph();
        var root = graph.AddRoot("root");

        var child1 = root.AddChild("child1");
        child1.Geometry = new CylinderGeometry(10, 100);
        child1.Color = Color.FromRgb(255, 0, 0);
        child1.LocalPose = new Pose3<double>(100, 200, 300, 0, 0, 0);

        var child2 = root.AddChild("child2");
        child2.Geometry = new SphereGeometry(5);
        child2.Color = Color.FromRgb(0, 255, 0);
        child2.LocalPose = new Pose3<double>(400, 500, 600, 45, 0, 0);
        child2.Visible = false;

        var options = Serialization.Scene3DJsonOptions.Default;

        // Act -- serialize and deserialize
        var json = System.Text.Json.JsonSerializer.Serialize(graph, options);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SceneGraph>(json, options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Roots.Count.Should().Be(1);

        var deserializedRoot = deserialized.Roots[0];
        deserializedRoot.Name.Should().Be("root");
        deserializedRoot.Children.Count.Should().Be(2);

        var dc1 = deserializedRoot.Children[0];
        dc1.Name.Should().Be("child1");
        dc1.Geometry.Should().BeOfType<CylinderGeometry>();
        var cyl = (CylinderGeometry)dc1.Geometry!;
        cyl.Radius.Should().Be(10);
        cyl.Height.Should().Be(100);
        dc1.LocalPose.X.Should().Be(100);
        dc1.LocalPose.Y.Should().Be(200);
        dc1.LocalPose.Z.Should().Be(300);
        dc1.Visible.Should().BeTrue();
        dc1.Color.Should().Be(Color.FromRgb(255, 0, 0));
        dc1.Opacity.Should().Be(1.0);

        var dc2 = deserializedRoot.Children[1];
        dc2.Name.Should().Be("child2");
        dc2.Geometry.Should().BeOfType<SphereGeometry>();
        ((SphereGeometry)dc2.Geometry!).Radius.Should().Be(5);
        dc2.Visible.Should().BeFalse();
        dc2.LocalPose.X.Should().Be(400);
        dc2.Color.Should().Be(Color.FromRgb(0, 255, 0));
        dc2.Opacity.Should().Be(1.0);
    }

    /// <summary>
    /// Test 3.5 -- Camera3D produces valid view parameters.
    /// Z-up convention. Target (0,0,0), distance 500, azimuth 45, elevation 30, FOV 60.
    /// Expected eye: (306.19, 306.19, 250.00) within 0.01 mm.
    /// </summary>
    [Fact]
    public void Camera3D_Should_ProduceValidEyePosition()
    {
        // Arrange
        var camera = new Camera3D
        {
            Target = Point3<double>.Zero,
            Distance = 500,
            AzimuthDegrees = 45,
            ElevationDegrees = 30,
            FieldOfViewDegrees = 60
        };

        // Act
        var eye = camera.ComputeEyePosition();
        var viewDir = camera.ComputeViewDirection();

        // Assert -- eye position within 0.01 mm
        eye.X.Should().BeApproximately(306.19, 0.01);
        eye.Y.Should().BeApproximately(306.19, 0.01);
        eye.Z.Should().BeApproximately(250.00, 0.01);

        // Assert -- view direction points from eye toward target (0,0,0)
        // View direction should have negative X, negative Y, negative Z components
        viewDir.X.Should().BeLessThan(0);
        viewDir.Y.Should().BeLessThan(0);
        viewDir.Z.Should().BeLessThan(0);
    }

    /// <summary>
    /// Test 3.6 -- Removing a parent node removes its children.
    /// </summary>
    [Fact]
    public void RemoveParent_Should_RemoveChildren()
    {
        // Arrange
        var graph = new SceneGraph();
        var root = graph.AddRoot("root");
        var parent = root.AddChild("parent");
        parent.AddChild("child1");
        parent.AddChild("child2");

        // Act
        root.RemoveChild("parent");

        // Assert
        graph.Find("parent").Should().BeNull();
        graph.Find("child1").Should().BeNull();
        graph.Find("child2").Should().BeNull();
        root.Children.Count.Should().Be(0);
    }
}
