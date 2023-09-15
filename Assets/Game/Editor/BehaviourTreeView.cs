#if UNITY_EDITOR

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

using BehaviorTreeAI;

public class BehaviourTreeView : GraphView
{
    public Action<NodeView> OnNodeSelected;

    public new class UxmlFactory : UxmlFactory<BehaviourTreeView, GraphView.UxmlTraits> { }

    private BehaviourTree tree;
    public BehaviourTreeView()
    {
        Insert(0, new GridBackground());

        this.AddManipulator( new ContentZoomer() );
        this.AddManipulator( new ContentDragger() );
        this.AddManipulator( new SelectionDragger() );
        this.AddManipulator( new RectangleSelector() );

        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>( BehaviourTreeEditor.editorPath + "BehaviourTreeEditor.uss");
        styleSheets.Add(styleSheet);

        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private NodeView FindNodeView( BehaviorTreeAI.Node node )
    {
        return GetNodeByGuid( node.guid ) as NodeView;
    }

    internal void PopulateView( BehaviourTree tree )
    {
        this.tree = tree;

        graphViewChanged -= OnGraphViewChanged;
        DeleteElements( graphElements.ToList() );
        graphViewChanged += OnGraphViewChanged;

        if ( tree.root == null )
        {
            tree.root = tree.CreateNode( typeof( Root ) ) as Root;
            EditorUtility.SetDirty( tree );
            AssetDatabase.SaveAssets();
        }

        tree.nodes.ForEach( n => CreateNodeView( n ) );
        tree.nodes.ForEach( n => {

            NodeView parentView = FindNodeView( n );

            var children = tree.GetChildren( n );

            if ( parentView.subOutput != null ) // Simple parallel node only
            {
                BehaviorTreeAI.Node mainAction = children[0];
                BehaviorTreeAI.Node subAction = children[1]; 

                NodeView mainActionView = FindNodeView( mainAction );
                Edge mainEdge = parentView.output.ConnectTo( mainActionView.input );
                AddElement( mainEdge );

                NodeView subActionView = FindNodeView( subAction );
                Edge subEdge = parentView.subOutput.ConnectTo( subActionView.input );
                AddElement( subEdge );
            }
            else
            {
                children.ForEach( c => {
                    NodeView childView = FindNodeView( c );

                    Edge edge = parentView.output.ConnectTo( childView.input );
                    AddElement( edge );
                });
            }

        } );
    }

    private void CreateNodeView( BehaviorTreeAI.Node node )
    {
        NodeView nodeView = new NodeView( node );
        nodeView.OnNodeSelected = OnNodeSelected;
        AddElement( nodeView );
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        {
            var types = TypeCache.GetTypesDerivedFrom<BehaviorTreeAI.Action>();
            foreach( var type in types )
            {
                evt.menu.AppendAction($"[{type.BaseType.Name}] {type.Name}", ( _ ) => CreateNode( type ) );
            }
        }

        {
            var types = TypeCache.GetTypesDerivedFrom<Composite>();
            foreach( var type in types )
            {
                evt.menu.AppendAction($"[{type.BaseType.Name}] {type.Name}", ( _ ) => CreateNode( type ) );
            }
        }

        {
            var types = TypeCache.GetTypesDerivedFrom<Decorator>();
            foreach( var type in types )
            {
                evt.menu.AppendAction($"[{type.BaseType.Name}] {type.Name}", ( _ ) => CreateNode( type ) );
            }
        }

    }

    private void CreateNode( System.Type type )
    {
        BehaviorTreeAI.Node node = tree.CreateNode( type );
        CreateNodeView( node );
    }

    private GraphViewChange OnGraphViewChanged( GraphViewChange graphViewChange )
    {
        if (graphViewChange.elementsToRemove != null)
        {
            graphViewChange.elementsToRemove.ForEach( element => {
                NodeView nodeView = element as NodeView;
                if ( nodeView != null )
                {
                    tree.DeleteNode( nodeView.node );
                }

                Edge edge = element as Edge;
                if ( edge != null )
                {
                    NodeView parentView = edge.output.node as NodeView;
                    NodeView childView = edge.input.node as NodeView;

                    tree.RemoveChild( parentView.node, childView.node );
                }
            });          
        }

        if ( graphViewChange.edgesToCreate != null )
        {
            graphViewChange.edgesToCreate.ForEach( edge => {
                NodeView parentView = edge.output.node as NodeView;
                NodeView childView = edge.input.node as NodeView;

                tree.AddChild( parentView.node, childView.node );
            });
        }

        if ( graphViewChange.movedElements != null )
        {
            nodes.ForEach( n => {
                NodeView view = n as NodeView;
                view.SortChildren();
            });
        }

        return graphViewChange;
    }

    public override List<Port> GetCompatiblePorts( Port startPort, NodeAdapter nodeAdapter )
    {
        return ports.ToList().Where( endPort => endPort.direction != startPort.direction && endPort.node != startPort.node).ToList();
    }

    private void OnUndoRedo()
    {
        PopulateView( tree );
        AssetDatabase.SaveAssets();
    }

    public void UpdateNodeState()
    {
        nodes.ForEach( n => {
            NodeView view = n as NodeView;
            view.UpdateState();
        });
    }
}
#endif
