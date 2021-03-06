﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RCT2ObjectData.DataObjects.Types;

namespace RCT2ObjectData.DataObjects {
/** <summary> The info header for all objects. </summary> */
public struct ObjectDataInfo {

	//=========== MEMBERS ============
	#region Members

	/** <summary> The 8 character file name of the object. </summary> */
	public string FileName;
	/** <summary> The flags of the object. </summary> */
	public uint Flags;
	/** <summary> The name of the object. </summary> */
	public string Name;
	/** <summary> The subtype of the object. </summary> */
	public ObjectSubtypes Subtype;
	/** <summary> The header of the object info. </summary> */
	public ObjectTypeHeader Header;
	/** <summary> The 8 checksum of the file. </summary> */
	public uint CheckSum;

	#endregion
	//========== PROPERTIES ==========
	#region Properties

	/** <summary> Gets the type of the object. </summary> */
	public SourceTypes Source {
		get { return (SourceTypes)((Flags >> 4) & 0xF); }
	}
	/** <summary> Gets the type of the object. </summary> */
	public ObjectTypes Type {
		get { return (ObjectTypes)(Flags & 0xF); }
	}
	/** <summary> Returns true if the object is invalid. </summary> */
	public bool Invalid {
		get { return Flags == 0xFFFFFFFF; }
	}

	#endregion
	//=========== READING ============
	#region Reading

	/** <summary> Returns an object info structure loaded from the specified stream. </summary> */
	public static ObjectDataInfo FromStream(Stream stream, bool readName) {
		ObjectDataInfo objInfo = new ObjectDataInfo();

		try {
			BinaryReader reader = new BinaryReader(stream, Encoding.Unicode);

			// Read the object data header
			objInfo.Flags = reader.ReadUInt32();
			objInfo.FileName = "";
			for (int i = 0; i < 8; i++) {
				char c = (char)reader.ReadByte();
				if (c != ' ')
					objInfo.FileName += c;
			}
			objInfo.CheckSum = reader.ReadUInt32();
			objInfo.Name = "";

			if (readName) {
				uint headerSize = 0x0;

				switch (objInfo.Type) {
				case ObjectTypes.Attraction: headerSize = Attraction.HeaderSize; break;
				case ObjectTypes.SmallScenery: headerSize = SmallScenery.HeaderSize; break;
				case ObjectTypes.LargeScenery: headerSize = LargeScenery.HeaderSize; break;
				case ObjectTypes.Wall: headerSize = Wall.HeaderSize; break;
				case ObjectTypes.PathBanner: headerSize = PathBanner.HeaderSize; break;
				case ObjectTypes.Path: headerSize = Pathing.HeaderSize; break;
				case ObjectTypes.PathAddition: headerSize = PathAddition.HeaderSize; break;
				case ObjectTypes.SceneryGroup: headerSize = SceneryGroup.HeaderSize; break;
				case ObjectTypes.ParkEntrance: headerSize = ParkEntrance.HeaderSize; break;
				case ObjectTypes.Water: headerSize = Water.HeaderSize; break;
				case ObjectTypes.ScenarioText: headerSize = 0x0; break;
				default: objInfo.Flags = 0xFFFFFFFF; break;
				}

				// Decode enough of the chunk to get the name
				if (headerSize != 0x0)
					ReadChunkInfo(reader, objInfo.Type, headerSize, ref objInfo);
			}
			reader.Close();
		}
		catch (System.Exception) {
			objInfo.Flags = 0xFFFFFFFF;
		}

		return objInfo;
	}
	/** <summary> Returns an object info structure loaded from the specified buffer. </summary> */
	public static ObjectDataInfo FromBuffer(byte[] buffer, bool readName) {
		return FromStream(new MemoryStream(buffer), readName);
	}
	/** <summary> Returns an object info structure loaded from the specified file path. </summary> */
	public static ObjectDataInfo FromFile(string path, bool readName) {
		return FromStream(new FileStream(path, FileMode.Open, FileAccess.Read), readName);
	}
	/** <summary> Reads and decodes the chunk. </summary> */
	internal static void ReadChunkInfo(BinaryReader reader, ObjectTypes type, uint headerSize, ref ObjectDataInfo objInfo) {
		ChunkEncoding encoding = (ChunkEncoding)reader.ReadByte();
		uint chunkSize = reader.ReadUInt32();
		uint chunkPosition = 0;
		uint position = 0;
		long startPos = reader.BaseStream.Position;

		MemoryStream stream = new MemoryStream();
		BinaryWriter writer = new BinaryWriter(stream);

		switch (encoding) {
		case ChunkEncoding.None:

			//Console.WriteLine("None");
			writer.Write(reader.ReadBytes((int)headerSize + 200));
			position += headerSize + 200;

			break;
		case ChunkEncoding.RLE:
		case ChunkEncoding.RLEString:

			//Console.WriteLine((encoding == EncodingType.RLE ? "RLE" : "RLEString"));
			//http://tid.rctspace.com/RLE.html

			// While the end of the uncompressed chunk has not been reached
			while (chunkPosition < (long)chunkSize && position < headerSize + 200) {
				// Read the next byte
				byte b = reader.ReadByte();

				// If the MSB is 0, copy the next (b + 1) bytes
				if ((b & 0x80) == 0) {
					uint length = (uint)(b + 1);
					chunkPosition += length + 1;
					position += length;
					//Console.WriteLine("Copy: " + b + " " + length + " Position: " + chunkPosition);
					writer.Write(reader.ReadBytes((int)length));
				}

				// Else the MSB is 1, repeat the following byte (-b + 1) times
				else {
					byte copyByte = reader.ReadByte();
					uint length = (uint)((byte)(-(sbyte)b) + 1);
					chunkPosition += 2;
					position += length;
					//Console.WriteLine("Repeat: " + b + " " + length + " " + copyByte + " Position: " + chunkPosition);
					for (var i = 0; i < length; i++)
						writer.Write(copyByte);
				}
			}

			// Decompress strings
			if (encoding == ChunkEncoding.RLEString) {
				Console.WriteLine("RLE String Encoding not supported!");
			}

			break;
		case ChunkEncoding.Rotate:
			Console.WriteLine("Rotate Encoding not supported!");
			break;
		}

		writer.Close();
		BinaryReader reader2 = new BinaryReader(new MemoryStream(stream.GetBuffer()));

		switch (type) {
		case ObjectTypes.Attraction: objInfo.Header = new AttractionHeader(); break;
		case ObjectTypes.SmallScenery: objInfo.Header = new SmallSceneryHeader(); break;
		case ObjectTypes.LargeScenery: objInfo.Header = new LargeSceneryHeader(); break;
		case ObjectTypes.Wall: objInfo.Header = new WallHeader(); break;
		case ObjectTypes.PathBanner: objInfo.Header = new PathBannerHeader(); break;
		case ObjectTypes.Path: objInfo.Header = new PathingHeader(); break;
		case ObjectTypes.PathAddition: objInfo.Header = new PathAdditionHeader(); break;
		case ObjectTypes.SceneryGroup: objInfo.Header = new SceneryGroupHeader(); break;
		case ObjectTypes.ParkEntrance: objInfo.Header = new ParkEntranceHeader(); break;
		case ObjectTypes.Water: objInfo.Header = new WaterHeader(); break;
		case ObjectTypes.ScenarioText: objInfo.Header = null; break;
		}
		if (objInfo.Header != null) {
			objInfo.Header.Read(reader2);
			objInfo.Subtype = objInfo.Header.ObjectSubtype;
		}
		else {
			reader2.ReadBytes((int)headerSize);
		}

		byte bs = reader2.ReadByte();
		objInfo.Name = "";
		// If the lanuage index is 0xFF, end the string table
		while (bs != 0xFF && bs < 2 && objInfo.Name.Replace(" ", "").Length == 0) {
			// Read the null-terminated string
			objInfo.Name = "";
			char c = (char)reader2.ReadByte();
			while (c != 0x00) {
				objInfo.Name += c;
				c = (char)reader2.ReadByte();
			}
			bs = reader2.ReadByte();
		}

		reader2.Close();
		stream.Close();
	}

	#endregion
}
/** <summary> The header for all objects. </summary> */
public class ObjectDataHeader {

	//=========== MEMBERS ============
	#region Members

	/** <summary> The 8 character file name of the object. </summary> */
	public string FileName;
	/** <summary> The flags of the object. </summary> */
	public uint Flags;
	/** <summary> The checksum of the object header. </summary> */
	public uint CheckSum;

	#endregion
	//=========== READING ============
	#region Reading

	/** <summary> Reads the object data header. </summary> */
	public void Read(BinaryReader reader) {
		this.Flags = reader.ReadUInt32();
		this.FileName = "";
		for (int i = 0; i < 8; i++) {
			char c = (char)reader.ReadByte();
			if (c != ' ')
				this.FileName += c;
		}
		this.CheckSum = reader.ReadUInt32();
	}
	/** <summary> Writes the object data header. </summary> */
	public void Write(BinaryWriter writer) {
		writer.Write(this.Flags);
		for (int i = 0; i < 8; i++) {
			if (i < this.FileName.Length)
				writer.Write((byte)this.FileName[i]);
			else
				writer.Write((byte)' ');
		}
		writer.Write(this.CheckSum);
	}
	/** <summary> Saves the object data to the specified file path. </summary> */
	public static ObjectDataHeader FromFile(string path) {
		ObjectDataHeader obj = new ObjectDataHeader();
		BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));
		obj.Read(reader);
		return obj;
	}

	#endregion
}
/** <summary> The base header for all object types. </summary> */
public abstract class ObjectTypeHeader {

	//========== PROPERTIES ==========
	#region Properties

	/** <summary> Gets the size of the object type header. </summary> */
	internal abstract uint HeaderSize { get; }
	/** <summary> Gets the basic subtype of the object. </summary> */
	internal abstract ObjectSubtypes ObjectSubtype { get; }

	#endregion
	//=========== READING ============
	#region Reading

	/** <summary> Reads the object type header. </summary> */
	internal abstract void Read(BinaryReader reader);
	/** <summary> Writes the object type header. </summary> */
	internal abstract void Write(BinaryWriter writer);

	#endregion
}
/** <summary> The header for chunk data. </summary> */
public class ChunkHeader {

	//=========== MEMBERS ============
	#region Members

	/** <summary> The encoding type used by the chunk. </summary> */
	public ChunkEncoding Encoding;
	/** <summary> The size of the chunk. </summary> */
	public uint ChunkSize;

	#endregion
	//=========== READING ============
	#region Reading

	/** <summary> Reads the chunk header. </summary> */
	internal void Read(BinaryReader reader) {
		this.Encoding = (ChunkEncoding)reader.ReadByte();
		this.ChunkSize = reader.ReadUInt32();
	}
	/** <summary> Writes the chunk header. </summary> */
	internal void Write(BinaryWriter writer) {
		writer.Write((byte)this.Encoding);
		writer.Write(this.ChunkSize);
	}

	#endregion
}
/** <summary> The base object data class. </summary> */
public class ObjectData {

	//=========== MEMBERS ============
	#region Members

	/** <summary> The header of the object data. </summary> */
	protected ObjectDataHeader objectHeader;
	/** <summary> The header of the chunk. </summary> */
	protected ChunkHeader chunkHeader;
	/** <summary> The string table of the object. </summary> */
	protected StringTable stringTable;
	/** <summary> The information about the object's group. </summary> */
	protected GroupInfo groupInfo;
	/** <summary> The image directory of the object. </summary> */
	protected ImageDirectory imageDirectory;
	/** <summary> The graphics data of the object. </summary> */
	protected GraphicsData graphicsData;

	#endregion
	//========= CONSTRUCTORS =========
	#region Constructors

	/** <summary> Constructs the base object data. </summary> */
	internal ObjectData() {
		this.objectHeader	= new ObjectDataHeader();
		this.chunkHeader	= new ChunkHeader();
		this.stringTable	= new StringTable();
		this.groupInfo		= new GroupInfo();
		this.imageDirectory	= new ImageDirectory();
		this.graphicsData	= new GraphicsData(this.imageDirectory);
	}
	/** <summary> Constructs the base object data. </summary> */
	internal ObjectData(ObjectDataHeader objectHeader, ChunkHeader chunkHeader) {
		this.objectHeader	= objectHeader;
		this.chunkHeader	= chunkHeader;
		this.stringTable	= new StringTable();
		this.groupInfo		= new GroupInfo();
		this.imageDirectory	= new ImageDirectory();
		this.graphicsData	= new GraphicsData(this.imageDirectory);
	}

	#endregion
	//========== PROPERTIES ==========
	#region Properties
	//--------------------------------
	#region Base

	/** <summary> Gets the header of the object. </summary> */
	public ObjectDataHeader ObjectHeader {
		get { return objectHeader; }
		set { objectHeader = value; }
	}
	/** <summary> Gets the header of the chunk. </summary> */
	public ChunkHeader ChunkHeader {
		get { return chunkHeader; }
		set { chunkHeader = value; }
	}
	/** <summary> The string table of the object. </summary> */
	public StringTable StringTable {
		get { return stringTable; }
	}
	/** <summary> The information about the object's group. </summary> */
	public GroupInfo GroupInfo {
		get { return groupInfo; }
	}
	/** <summary> The image directory of the object. </summary> */
	public ImageDirectory ImageDirectory {
		get { return imageDirectory; }
	}
	/** <summary> The graphics data of the object. </summary> */
	public GraphicsData GraphicsData {
		get { return graphicsData; }
	}
	/** <summary> Gets the type of the object. </summary> */
	public SourceTypes Source {
		get { return (SourceTypes)((objectHeader.Flags >> 4) & 0xF); }
		set { objectHeader.Flags &= 0xFFFFFF0F; objectHeader.Flags |= (uint)((byte)value << 4) & 0xF0; }
	}
	/** <summary> Gets the type of the object. </summary> */
	public ObjectTypes Type {
		get { return (ObjectTypes)(objectHeader.Flags & 0xF); }
	}
	/** <summary> Returns true if the object is invalid. </summary> */
	public bool Invalid {
		get { return (ObjectTypes)(objectHeader.Flags & 0xF) == ObjectTypes.None; }
	}

	#endregion
	//--------------------------------
	#region Reading

	/** <summary> Gets the number of string table entries in the object. </summary> */
	protected virtual int NumStringTableEntries {
		get { return 1; }
	}
	/** <summary> Returns true if the object has a group info section. </summary> */
	protected virtual bool HasGroupInfo {
		get { return false; }
	}

	#endregion
	//--------------------------------
	#region Information

	/** <summary> Gets the subtype of the object. </summary> */
	public virtual ObjectSubtypes Subtype {
		get { return ObjectSubtypes.Basic; }
	}
	/** <summary> True if the object can be placed on a slope. </summary> */
	public virtual bool CanSlope {
		get { return false; }
	}
	/** <summary> Gets the number of color remaps. </summary> */
	public virtual int ColorRemaps {
		get { return 0; }
	}
	/** <summary> Gets if the dialog view has color remaps. </summary> */
	public virtual bool HasDialogColorRemaps {
		get { return false; }
	}
	/** <summary> Gets the number of frames in the animation. </summary> */
	public virtual int AnimationFrames {
		get { return 1; }
	}
	/** <summary> Gets the palette to draw the object with. </summary> */
	public virtual Palette GetPalette(DrawSettings drawSettings) {
		return Palette.DefaultPalette;
	}

	#endregion
	//--------------------------------
	#endregion
	//=========== READING ============
	#region Reading

	/** <summary> Reads the object data. </summary> */
	protected void Read(BinaryReader reader, bool quickLoad = false) {
		// Read the header
		ReadHeader(reader);

		// Read the string table entries
		stringTable.Read(reader, NumStringTableEntries);
		// Read the group info
		if (HasGroupInfo)
			groupInfo.Read(reader);

		// Read the optional section
		ReadOptional(reader);

		// Read the image directory
		imageDirectory.Read(reader, quickLoad);
		// Read the graphics data
		graphicsData.Read(reader);
	}
	/** <summary> Writes the object data. </summary> */
	protected void Write(BinaryWriter writer) {
		// Write the header
		WriteHeader(writer);

		// Write the string table entries
		stringTable.Write(writer);
		// Write the group info
		if (HasGroupInfo)
			groupInfo.Write(writer);

		// Write the optional section
		WriteOptional(writer);

		long imageDirectoryPosition = writer.BaseStream.Position;

		// Write the image directory and graphics data
		imageDirectory.Write(writer);
		graphicsData.Write(writer);

		// Rewrite the image directory after the image addresses are known
		long finalPosition = writer.BaseStream.Position;
		writer.BaseStream.Position = imageDirectoryPosition;
		imageDirectory.Write(writer);

		// Set the position to the end of the file so the file size is known
		writer.BaseStream.Position = finalPosition;
	}
	/** <summary> Reads the object data header. </summary> */
	protected virtual void ReadHeader(BinaryReader reader) {
		
	}
	/** <summary> Reads the object data optional. </summary> */
	protected virtual void ReadOptional(BinaryReader reader) {

	}
	/** <summary> Writes the object data header. </summary> */
	protected virtual void WriteHeader(BinaryWriter writer) {

	}
	/** <summary> Writes the object data optional. </summary> */
	protected virtual void WriteOptional(BinaryWriter writer) {

	}

	#endregion
	//=========== DRAWING ============
	#region Drawing

	/** <summary> Draws the object as it is in game. </summary> */
	public virtual bool Draw(PaletteImage p, Point position, DrawSettings drawSettings) {
		return false;
	}
	/** <summary> Draws the object as it is in the dialog. </summary> */
	public virtual bool DrawDialog(PaletteImage p, Point position, Size dialogSize, DrawSettings drawSettings) {
		return false;
	}

	#endregion
	//============ STATIC ============
	#region Static

	/** <summary> Saves the object data to the specified file path. </summary> */
	public void Save(string path) {
		try {
			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream);

			this.objectHeader.Write(writer);
			this.chunkHeader.Write(writer);

			long chunkStartPosition = writer.BaseStream.Position;
			this.Write(writer);
			// Set the chunk size
			this.chunkHeader.ChunkSize = (uint)(writer.BaseStream.Position - chunkStartPosition);
			// Get the file size
			uint fileSize = (uint)writer.BaseStream.Position;

			BinaryReader reader = new BinaryReader(stream);
			reader.BaseStream.Position = 0;

			// Calculate the checksum
			uint checkSum = 0xF369A75B;
			checkSum = RotateChecksum(checkSum, reader.ReadByte());
			reader.ReadBytes(3);
			for (int i = 0; i < 8; i++)
				checkSum = RotateChecksum(checkSum, reader.ReadByte());
			reader.ReadBytes(9);
			for (int i = 16 + 5; i < (int)fileSize; i++)
				checkSum = RotateChecksum(checkSum, reader.ReadByte());

			this.objectHeader.CheckSum = checkSum;

			byte[] chunkData = new byte[fileSize - 21];
			Array.Copy(stream.GetBuffer(), 21, chunkData, 0, fileSize - 21);

			writer.Close();
			reader.Close();

			writer = new BinaryWriter(new FileStream(path, FileMode.Create));

			byte[] encodedChunkData = WriteChunk(this.chunkHeader, chunkData);

			this.objectHeader.Write(writer);
			this.chunkHeader.Write(writer);
			writer.Write(encodedChunkData);

			writer.Close();
		}
		catch (System.Exception) {

		}
	}
	/** <summary> Returns an object loaded from the specified stream. </summary> */
	public static ObjectData FromStream(Stream stream, bool quickLoad = false) {
		ObjectData obj = null;
		ObjectDataHeader objectHeader = new ObjectDataHeader();
		ChunkHeader chunkHeader = new ChunkHeader();

		try {
			BinaryReader reader = new BinaryReader(stream, Encoding.Unicode);
			objectHeader.Read(reader);

			byte[] data = null;
			if ((ObjectTypes)(objectHeader.Flags & 0xF) != ObjectTypes.ScenarioText) {
				data = ObjectData.ReadChunk(reader, chunkHeader);
				reader.Close();
				reader = new BinaryReader(new MemoryStream(data));
			}

			switch ((ObjectTypes)(objectHeader.Flags & 0xF)) {
			case ObjectTypes.Attraction: obj = new Attraction(objectHeader, chunkHeader); break;
			case ObjectTypes.SmallScenery: obj = new SmallScenery(objectHeader, chunkHeader); break;
			case ObjectTypes.LargeScenery: obj = new LargeScenery(objectHeader, chunkHeader); break;
			case ObjectTypes.Wall: obj = new Wall(objectHeader, chunkHeader); break;
			case ObjectTypes.PathBanner: obj = new PathBanner(objectHeader, chunkHeader); break;
			case ObjectTypes.Path: obj = new Pathing(objectHeader, chunkHeader); break;
			case ObjectTypes.PathAddition: obj = new PathAddition(objectHeader, chunkHeader); break;
			case ObjectTypes.SceneryGroup: obj = new SceneryGroup(objectHeader, chunkHeader); break;
			case ObjectTypes.ParkEntrance: obj = new ParkEntrance(objectHeader, chunkHeader); break;
			case ObjectTypes.Water: obj = new Water(objectHeader, chunkHeader); break;
			case ObjectTypes.ScenarioText:	/*obj = new ScenarioText(objectHeader, chunkHeader);*/	break;
			default: objectHeader.Flags = (uint)ObjectTypes.None; break; // Set as invalid
			}
			if (obj != null) {
				obj.Read(reader, quickLoad);
			}
			reader.Close();
		}
		catch (System.Exception) {
			objectHeader.Flags = (uint)ObjectTypes.None;
			obj = new ObjectData(objectHeader, chunkHeader);
		}

		return obj;
	}
	/** <summary> Returns an object loaded from the specified file path. </summary> */
	public static ObjectData FromFile(string path, bool quickLoad = false) {
		return FromStream(new FileStream(path, FileMode.Open, FileAccess.Read), quickLoad);
	}
	/** <summary> Returns an object loaded from the specified buffer. </summary> */
	public static ObjectData FromBuffer(byte[] buffer, bool quickLoad = false) {
		return FromStream(new MemoryStream(buffer), quickLoad);
	}
	/** <summary> Reads and decodes the chunk. </summary> */
	public static byte[] ReadChunk(BinaryReader reader, ChunkHeader chunkHeader) {
		ChunkEncoding encoding = (ChunkEncoding)reader.ReadByte();
		uint chunkSize = reader.ReadUInt32();
		uint chunkPosition = 0;
		uint position = 0;
		long startPos = reader.BaseStream.Position;

		chunkHeader.Encoding = encoding;
		chunkHeader.ChunkSize = chunkSize;

		MemoryStream stream = new MemoryStream();
		BinaryWriter writer = new BinaryWriter(stream);

		switch (encoding) {
		case ChunkEncoding.None:

			//Console.WriteLine("None");
			writer.Write(reader.ReadBytes((int)chunkSize));
			position += chunkSize;

			break;
		case ChunkEncoding.RLE:
		case ChunkEncoding.RLEString:

			//http://tid.rctspace.com/RLE.html

			// While the end of the uncompressed chunk has not been reached
			while (chunkPosition < (long)chunkSize) {
				// Read the next byte
				byte b = reader.ReadByte();

				// If the MSB is 0, copy the next (b + 1) bytes
				if ((b & 0x80) == 0) {
					uint length = (uint)(b + 1);
					chunkPosition += length + 1;
					position += length;
					//Console.WriteLine("Copy: " + b + " " + length + " Position: " + chunkPosition);
					writer.Write(reader.ReadBytes((int)length));
				}

				// Else the MSB is 1, repeat the following byte (-b + 1) times
				else {
					byte copyByte = reader.ReadByte();
					uint length = (uint)((byte)(-(sbyte)b) + 1);
					chunkPosition += 2;
					position += length;
					//Console.WriteLine("Repeat: " + b + " " + length + " " + copyByte + " Position: " + chunkPosition);
					for (var i = 0; i < length; i++)
						writer.Write(copyByte);
				}
			}

			// Decompress strings
			if (encoding == ChunkEncoding.RLEString) {
				Console.WriteLine("RLE String Encoding not supported!");
			}

			break;
		case ChunkEncoding.Rotate:
			Console.WriteLine("Rotate Encoding not supported!");
			break;
		}

		byte[] data = new byte[position];
		Array.Copy(stream.GetBuffer(), data, position);
		return data;
	}
	/** <summary> Reads and decodes the chunk. </summary> */
	public static byte[] WriteChunk(ChunkHeader header, byte[] data) {
		uint position = 0;
		uint chunkPosition = 0;

		switch (header.Encoding) {
		case ChunkEncoding.None:

			header.ChunkSize = (uint)data.Length;
			return data;
			//writer.Write(data);

		case ChunkEncoding.RLE:
		case ChunkEncoding.RLEString:

			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream);

			while (position < data.Length) {

				uint startLength = position;
				if (position + 1 == data.Length) {
					writer.Write((byte)0x00);
					writer.Write(data[position]);
					position++;
					chunkPosition += 2;
				}
				else {
					bool duplicate = false;
					byte startByte = data[position];
					byte b = data[position + 1];
					if (b == startByte)
						duplicate = true;
					int count = 2;
					for (; count < 125 && count + position < data.Length; count++) {
						if ((b == data[position + count]) != duplicate) {
							if (!duplicate)
								count--;
							break;
						}
						else {
							b = data[position + count];
						}
					}

					if (!duplicate) {
						writer.Write((byte)(count - 1));
						for (int i = 0; i < count; i++) {
							writer.Write(data[position + i]);
						}
						chunkPosition += (uint)(count + 1);
					}
					else {
						writer.Write((byte)(-(sbyte)((byte)count - 1)));
						writer.Write(startByte);
						chunkPosition += 2;
					}
						
					position += (uint)count;
				}
			}
			header.ChunkSize = chunkPosition;


			byte[] data2 = new byte[chunkPosition];
			Array.Copy(stream.GetBuffer(), data2, chunkPosition);
			return data2;

		case ChunkEncoding.Rotate:

			Console.WriteLine("Rotate");
			break;
		}

		return null;
	}
	private static uint RotateChecksum(uint checksum, byte data) {
		byte checkSumByte = (byte)(checksum & 0xFF);
		checksum &= 0xFFFFFF00;
		checksum |= (uint)(checkSumByte ^ data);
		//checksum ^= (uint)data;
		checksum = (checksum << 11) | (checksum >> (32 - 11));
		return checksum;
	}

	#endregion
}
/** <summary> The type of objects. </summary> */
public enum ObjectTypes : byte {
	/** <summary> No object type. </summary> */
	None = 0xF,
	/** <summary> The object is a ride or shop. </summary> */
	Attraction = 0,
	/** <summary> The object is a small scenery. </summary> */
	SmallScenery = 1,
	/** <summary> The object is a large scenery. </summary> */
	LargeScenery = 2,
	/** <summary> The object is a wall. </summary> */
	Wall = 3,
	/** <summary> The object is a path banner. </summary> */
	PathBanner = 4,
	/** <summary> The object is a path. </summary> */
	Path = 5,
	/** <summary> The object is a path addition. </summary> */
	PathAddition = 6,
	/** <summary> The object is a scenery group. </summary> */
	SceneryGroup = 7,
	/** <summary> The object is a park entrance. </summary> */
	ParkEntrance = 8,
	/** <summary> The object is a water palette. </summary> */
	Water = 9,
	/** <summary> The object is scenario text. </summary> */
	ScenarioText = 10
}
/** <summary> The subtype of objects. </summary> */
public enum ObjectSubtypes : byte {

	//--------------------------------
	#region Basic

	None,
	Basic,

	Water,
	Entrance,
	Group,
	Path,
	
	#endregion
	//--------------------------------
	#region Attractions

	TransportRide,
	GentleRide,
	Rollercoaster,
	ThrillRide,
	WaterRide,
	Stall,

	#endregion
	//--------------------------------
	#region Small Scenery

	Fountain,
	Clock,
	Garden,
	
	#endregion
	//--------------------------------
	#region Walls
	
	Door,
	
	#endregion
	//--------------------------------
	#region Multiple Types
	
	Animation,
	Glass,
	
	#endregion
	//--------------------------------
	#region Path Additions

	Lamp,
	LitterBin,
	Bench,
	JumpingFountain,
	QueueTV,

	#endregion
	//--------------------------------
	#region Signs

	Text3D,
	TextScrolling,
	Photogenic,
	
	#endregion
}
/** <summary> The type of source this object came from. </summary> */
public enum SourceTypes : byte {
	/** <summary> No object type. </summary> */
	RCT2 = 8,
	/** <summary> The object is a ride or shop. </summary> */
	WW = 1,
	/** <summary> The object is a small scenery. </summary> */
	TT = 2,
	/** <summary> The object is a large scenery. </summary> */
	Custom = 0
}
/** <summary> The type of encoding used in chunks. </summary> */
public enum ChunkEncoding : byte {
	/** <summary> There is no encoding. </summary> */
	None = 0,
	/** <summary> Uses Run Length Encoding. </summary> */
	RLE = 1,
	/** <summary> Uses Run Length Encoding then String Decompression. NOT SUPPORTED. </summary> */
	RLEString = 2,
	/** <summary> Uses Rotate Encoding. NOT SUPPORTED. </summary> */
	Rotate = 3
}
}
