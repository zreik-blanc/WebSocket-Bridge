locals {
  my_current_ip = "${chomp(data.http.myip.response_body)}/32"
}