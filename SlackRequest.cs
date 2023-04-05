#nullable disable

class SlackRequest
{
  public string token { get; set; }
  public string team_id { get; set; }
  public string team_domain { get; set; }
  public string enterprise_id { get; set; }
  public string enterprise_name { get; set; }
  public string channel_id { get; set; }
  public string channel_name { get; set; }
  public string user_id { get; set; }
  public string command { get; set; }
  public string text { get; set; }
  public string response_url { get; set; }
  public string trigger_id { get; set; }
  public string api_app_id { get; set; }
}

class SlackImageBlock
{
  public string type { get; set; } = "image";
  public string image_url { get; set; }
  public string alt_text { get; set; }
}

class SlackResponse
{
  public List<SlackImageBlock> blocks { get; set; } = new();
}
