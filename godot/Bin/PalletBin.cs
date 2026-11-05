using Godot;
using static PalletDimensions;

/// <summary>The visible pallet: deck, blocks and stacked collars with metal corners. It has no physics.</summary>
public partial class PalletBin : Node3D
{
    private const int DeckBoards = 7;
    private const float DeckHeight = 0.022f;
    private const float CollarSeam = 0.004f;
    private const float CornerPlateSize = 0.09f;
    private const float CornerPlateThickness = 0.003f;

    private static readonly Color WoodColor = new(0.72f, 0.56f, 0.36f);
    private static readonly Color MetalColor = new(0.55f, 0.57f, 0.6f);

    public override void _Ready()
    {
        BuildDeck();
        BuildBlocks();
        for (int i = 0; i < CollarCount; i++)
        {
            BuildCollar(i * CollarHeight);
        }
    }

    private void BuildDeck()
    {
        float boardWidth = CollarOuterWidth / DeckBoards * 0.86f;
        for (int i = 0; i < DeckBoards; i++)
        {
            float z = ((i + 0.5f) / DeckBoards - 0.5f) * CollarOuterWidth;
            AddBox(new Vector3(CollarOuterLength, DeckHeight, boardWidth), new Vector3(0f, -DeckHeight / 2f, z), WoodColor);
        }

        // A closed floor, because the links rest on the deck.
        AddBox(new Vector3(InnerLength, 0.002f, InnerWidth), new Vector3(0f, -0.001f, 0f), WoodColor.Darkened(0.25f));
    }

    private void BuildBlocks()
    {
        float blockHeight = PalletHeight - DeckHeight;
        foreach (float x in new[] { -0.55f, 0f, 0.55f })
        {
            foreach (float z in new[] { -0.35f, 0f, 0.35f })
            {
                AddBox(new Vector3(0.1f, blockHeight, 0.1f), new Vector3(x, -DeckHeight - blockHeight / 2f, z), WoodColor.Darkened(0.15f));
            }
        }
    }

    private void BuildCollar(float bottom)
    {
        float height = CollarHeight - CollarSeam;
        float y = bottom + CollarSeam + height / 2f;
        float offsetX = (CollarOuterLength - PlankThickness) / 2f;
        float offsetZ = (CollarOuterWidth - PlankThickness) / 2f;

        AddBox(new Vector3(CollarOuterLength, height, PlankThickness), new Vector3(0f, y, offsetZ), WoodColor);
        AddBox(new Vector3(CollarOuterLength, height, PlankThickness), new Vector3(0f, y, -offsetZ), WoodColor);
        AddBox(new Vector3(PlankThickness, height, InnerWidth), new Vector3(offsetX, y, 0f), WoodColor.Darkened(0.08f));
        AddBox(new Vector3(PlankThickness, height, InnerWidth), new Vector3(-offsetX, y, 0f), WoodColor.Darkened(0.08f));
        BuildCorners(y, height);
    }

    private void BuildCorners(float y, float collarHeight)
    {
        float plateHeight = collarHeight * 0.75f;
        foreach (float sx in new[] { -1f, 1f })
        {
            foreach (float sz in new[] { -1f, 1f })
            {
                float faceX = sx * (CollarOuterLength / 2f + CornerPlateThickness / 2f);
                float faceZ = sz * (CollarOuterWidth / 2f + CornerPlateThickness / 2f);
                AddBox(new Vector3(CornerPlateThickness, plateHeight, CornerPlateSize),
                    new Vector3(faceX, y, sz * (CollarOuterWidth / 2f - CornerPlateSize / 2f)), MetalColor);
                AddBox(new Vector3(CornerPlateSize, plateHeight, CornerPlateThickness),
                    new Vector3(sx * (CollarOuterLength / 2f - CornerPlateSize / 2f), y, faceZ), MetalColor);
            }
        }
    }

    private void AddBox(Vector3 size, Vector3 position, Color color)
    {
        AddChild(new MeshInstance3D
        {
            Position = position,
            Mesh = new BoxMesh
            {
                Size = size,
                Material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f },
            },
        });
    }
}
