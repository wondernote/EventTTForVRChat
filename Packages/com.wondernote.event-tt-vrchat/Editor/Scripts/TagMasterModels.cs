using System;
using System.Collections.Generic;

[Serializable]
public class TagMasterRoot
{
    public string version;
    public List<TagMasterRootGroup> rootGroups;
}

[Serializable]
public class TagMasterRootGroup
{
    public int id;
    public string label;
    public int sortOrder;
    public List<TagMasterChildGroup> children;
}

[Serializable]
public class TagMasterChildGroup
{
    public int id;
    public string label;
    public int sortOrder;
    public List<TagMasterTag> tags;
}

[Serializable]
public class TagMasterTag
{
    public int id;
    public string label;
    public int sortOrder;
}