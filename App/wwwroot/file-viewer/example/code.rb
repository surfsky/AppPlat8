class PreviewJob
  attr_reader :filename, :format

  def initialize(filename)
    @filename = filename
    @format = File.extname(filename).delete_prefix('.')
  end

  def async?
    %w[pdf docx dwg parquet].include?(format)
  end
end
